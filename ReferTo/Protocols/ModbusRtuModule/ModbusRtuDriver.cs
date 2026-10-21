using CommonInterfaces;
using CxVisual.Platform.VariableObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace CxVisual.Platform.Protocols.ModbusRtu
{
    public class ModbusRtuDriver : DriverModuleBase
    {
        private sealed class ModbusReadPlan
        {
            public ModbusRegisterType RegisterType { get; set; }

            public int ReadInterval { get; set; }

            public ushort StartAddress { get; set; }

            public ushort Quantity { get; set; }

            public long LastReadTime { get; set; }

            public List<ModbusRtuPointItem> Points { get; } = new List<ModbusRtuPointItem>();
        }

        public enum ModbusModuleState
        {
            Stopping,
            Stopped,
            Started
        }

        [Browsable(false)]
        public SerialPort SerialPort { get; private set; }

        [Browsable(false)]
        public ModbusRtuDriverModuleConfig ModbusConfig { get; private set; }

        public event EventHandler<ModbusModuleState> ModuleStateChanged;

        private volatile ModbusModuleState _moduleState = ModbusModuleState.Stopped;
        public ModbusModuleState ModuleState
        {
            get => _moduleState;
            private set
            {
                if (_moduleState != value)
                {
                    _moduleState = value;
                    ModuleStateChanged?.Invoke(this, value);
                }
            }
        }

        private readonly object _requestSyncLock = new object();
        private readonly object _serialPortLock = new object();

        private Thread _readingThread;
        private volatile bool _isReading;
        private List<ModbusReadPlan> _readPlans = new List<ModbusReadPlan>();

        public long ReadTime = 0;

        public ModbusRtuDriver(DriverModuleConfig config) : base(config)
        {
            ModbusConfig = config as ModbusRtuDriverModuleConfig;
            if (ModbusConfig == null)
            {
                throw new ArgumentException($"{config.Name} config is not a ModbusRtuDriverModuleConfig.");
            }
        }

        public override void StartReadDynamicProperty()
        {
            if (ModuleState != ModbusModuleState.Stopped)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ModbusConfig.PortName))
            {
                LogHelper.Error($"{Config.Name} has an empty Modbus RTU port name.");
                return;
            }

            var modbusPoints = GetModbusPoints();
            CheckModbusPoints(modbusPoints);
            _readPlans = BuildReadPlans(modbusPoints);

            OpenSerialPort();
            StartReadLoop();
            ModuleState = ModbusModuleState.Started;
        }

        public override void StopReadDynamicProperty()
        {
            if (ModuleState != ModbusModuleState.Started)
            {
                return;
            }

            ModuleState = ModbusModuleState.Stopping;
            _isReading = false;
            _readingThread?.Join();
            _readingThread = null;
            CloseSerialPort();
            ModuleState = ModbusModuleState.Stopped;
        }

        public bool GetChannelIsConnected()
        {
            lock (_serialPortLock)
            {
                return SerialPort != null && SerialPort.IsOpen;
            }
        }

        public List<ModbusRtuPointItem> GetModbusPoints()
        {
            var result = new List<ModbusRtuPointItem>();
            foreach (var point in PointSheet.Points)
            {
                if (point is ModbusRtuPointItem modbusPoint)
                {
                    result.Add(modbusPoint);
                }
            }

            return result;
        }

        protected override bool PointFromContextImpl(Dictionary<string, string> context, PointItem pointItem)
        {
            if (pointItem is ModbusRtuPointItem modbusPoint)
            {
                modbusPoint.RegisterType = (ModbusRegisterType)Enum.Parse(typeof(ModbusRegisterType), context["RegisterType"], true);
                modbusPoint.ValueType = (ModbusValueType)Enum.Parse(typeof(ModbusValueType), context["ValueType"], true);
                modbusPoint.Address = context["Address"];

                if (context.TryGetValue("ReadInterval", out string readIntervalText) && !string.IsNullOrWhiteSpace(readIntervalText))
                {
                    modbusPoint.ReadInterval = int.Parse(readIntervalText);
                }
                else
                {
                    modbusPoint.ReadInterval = ModbusConfig.ReadInterval;
                }

                return true;
            }

            return false;
        }

        protected override bool PointToContextImpl(Dictionary<string, string> context, PointItem pointItem)
        {
            if (pointItem is ModbusRtuPointItem modbusPoint)
            {
                context["RegisterType"] = modbusPoint.RegisterType.ToString();
                context["ValueType"] = modbusPoint.ValueType.ToString();
                context["Address"] = modbusPoint.Address;
                context["ReadInterval"] = modbusPoint.ReadInterval.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }

        protected override void LoadPointsAfter()
        {
            foreach (var point in GetModbusPoints())
            {
                if (VariableObjectManager.GetObject(point.DeviceName) == null)
                {
                    ModbusRtuPointItem.CreateAndRegisterDevice(point.DeviceName, point.ValueType);
                }
            }
        }

        public override PointItem CreatePointItem(string deviceName, string variableName)
        {
            return new ModbusRtuPointItem(deviceName, variableName);
        }

        protected override SendPointValueResult DoSendPointValue<TValue>(PointItem pointItem, TValue value)
        {
            if (!(pointItem is ModbusRtuPointItem modbusPoint))
            {
                return SendPointValueResult.InternalError;
            }

            if (!GetChannelIsConnected())
            {
                return SendPointValueResult.SendFailed;
            }

            try
            {
                ModbusRtuFrame request;
                if (modbusPoint.RegisterType == ModbusRegisterType.Coil)
                {
                    bool coilValue = ConvertToBool(value);
                    request = ModbusRtuProtocol.BuildWriteSingleCoilRequest(ModbusConfig.SlaveId, modbusPoint.RegisterAddress, coilValue);
                }
                else if (modbusPoint.RegisterType == ModbusRegisterType.HoldingRegister)
                {
                    ushort[] registers = BuildRegisterValuesForWrite(modbusPoint, value, out string buildError);
                    if (registers == null)
                    {
                        LogHelper.Error($"{Config.Name} failed to build write values for [{modbusPoint.DeviceName}.{modbusPoint.VariableName}]: {buildError}");
                        return SendPointValueResult.InternalError;
                    }

                    request = registers.Length == 1
                        ? ModbusRtuProtocol.BuildWriteSingleRegisterRequest(ModbusConfig.SlaveId, modbusPoint.RegisterAddress, registers[0])
                        : ModbusRtuProtocol.BuildWriteMultipleRegistersRequest(ModbusConfig.SlaveId, modbusPoint.RegisterAddress, registers);
                }
                else
                {
                    LogHelper.Error($"{Config.Name} cannot write point [{modbusPoint.DeviceName}.{modbusPoint.VariableName}] in register type [{modbusPoint.RegisterType}].");
                    return SendPointValueResult.InternalError;
                }

                if (!TryExecuteRequest(request, out ModbusRtuFrame response, out string errorMessage))
                {
                    LogHelper.Error($"{Config.Name} failed to write point [{modbusPoint.DeviceName}.{modbusPoint.VariableName}]: {errorMessage}");
                    return SendPointValueResult.SendFailed;
                }

                return response != null ? SendPointValueResult.SentAndAcknowledged : SendPointValueResult.SendFailed;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"{Config.Name} exception while writing point [{modbusPoint.DeviceName}.{modbusPoint.VariableName}]: {ex.Message}");
                return SendPointValueResult.SendFailed;
            }
        }

        protected override (SendPointValueResult, TValue) DoReadPointValue<TValue>(PointItem pointItem, TValue defalutValue)
        {
            if (!(pointItem is ModbusRtuPointItem modbusPoint))
            {
                return (SendPointValueResult.InternalError, defalutValue);
            }

            if (TryReadPointValue(modbusPoint, out object pointValue, out string errorMessage))
            {
                try
                {
                    if (pointValue is TValue typedValue)
                    {
                        return (SendPointValueResult.SentAndAcknowledged, typedValue);
                    }

                    TValue convertedValue = (TValue)Convert.ChangeType(pointValue, typeof(TValue), CultureInfo.InvariantCulture);
                    return (SendPointValueResult.SentAndAcknowledged, convertedValue);
                }
                catch
                {
                    return (SendPointValueResult.SentAndAcknowledged, defalutValue);
                }
            }

            LogHelper.Error($"{Config.Name} failed to read point [{modbusPoint.DeviceName}.{modbusPoint.VariableName}]: {errorMessage}");
            return (SendPointValueResult.SendFailed, defalutValue);
        }

        [Obfuscation(Exclude = true)]
        public void CheckModbusPoints(List<ModbusRtuPointItem> modbusPoints)
        {
            var duplicates = modbusPoints
                .GroupBy(item => new { item.RegisterType, item.RegisterAddress, item.BitOffset, item.ValueType })
                .Where(group => group.Count() > 1)
                .SelectMany(group => group.Skip(1))
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new ArgumentException($"{Config.Name} point table contains duplicate Modbus definitions.");
            }

            foreach (var point in modbusPoints)
            {
                string checkResult = ModbusRtuPointItem.CheckModbusPoint(point);
                if (!string.IsNullOrEmpty(checkResult))
                {
                    throw new ArgumentException($"{Config.Name} point [{point.DeviceName}] validation failed: {checkResult}");
                }
            }
        }

        public static void RegisterFactory()
        {
            DriverModuleFactoryCenter.RegisterFactory(
                StringHelper.ExtractShortNameAfterDot(typeof(ModbusRtuDriver).Name),
                "Modbus RTU Driver Module",
                driverModuleConfig => new ModbusRtuDriver(driverModuleConfig));
        }

        private void OpenSerialPort()
        {
            lock (_serialPortLock)
            {
                if (SerialPort != null)
                {
                    if (!SerialPort.IsOpen)
                    {
                        SerialPort.Open();
                    }
                    return;
                }

                SerialPort = new SerialPort
                {
                    PortName = ModbusConfig.PortName,
                    BaudRate = ModbusConfig.BaudRate,
                    DataBits = ModbusConfig.DataBits,
                    Parity = ModbusConfig.Parity,
                    StopBits = ModbusConfig.StopBits,
                    ReadTimeout = Math.Max(ModbusConfig.ResponseTimeout, 100),
                    WriteTimeout = Math.Max(ModbusConfig.ResponseTimeout, 100)
                };
                SerialPort.Open();
            }
        }

        private void CloseSerialPort()
        {
            lock (_serialPortLock)
            {
                if (SerialPort == null)
                {
                    return;
                }

                try
                {
                    if (SerialPort.IsOpen)
                    {
                        SerialPort.Close();
                    }
                }
                finally
                {
                    SerialPort.Dispose();
                    SerialPort = null;
                }
            }
        }

        private void StartReadLoop()
        {
            if (_isReading)
            {
                return;
            }

            _isReading = true;
            _readingThread = new Thread(() =>
            {
                while (_isReading)
                {
                    if (!GetChannelIsConnected())
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    long now = TimeHelper.GetNow();
                    foreach (var readPlan in _readPlans)
                    {
                        if (!_isReading)
                        {
                            break;
                        }

                        if (readPlan.ReadInterval <= 0)
                        {
                            continue;
                        }

                        if (now - readPlan.LastReadTime < readPlan.ReadInterval)
                        {
                            continue;
                        }

                        readPlan.LastReadTime = now;
                        ReadPlan(readPlan);
                    }

                    Thread.Sleep(10);
                }
            })
            { IsBackground = true };

            _readingThread.Start();
        }

        private void ReadPlan(ModbusReadPlan readPlan)
        {
            try
            {
                ModbusRtuFrame request = ModbusRtuProtocol.BuildReadRequest(
                    ModbusConfig.SlaveId,
                    readPlan.RegisterType,
                    readPlan.StartAddress,
                    readPlan.Quantity);

                if (!TryExecuteRequest(request, out ModbusRtuFrame response, out string errorMessage))
                {
                    LogHelper.Debug($"{Config.Name} periodic read [{readPlan.RegisterType}] {readPlan.StartAddress}-{readPlan.StartAddress + readPlan.Quantity - 1} failed: {errorMessage}");
                    return;
                }

                if (readPlan.RegisterType == ModbusRegisterType.Coil || readPlan.RegisterType == ModbusRegisterType.DiscreteInput)
                {
                    ApplyBitReadPlan(readPlan, ModbusRtuProtocol.ParseBitsFromReadResponse(response));
                }
                else
                {
                    ApplyRegisterReadPlan(readPlan, ModbusRtuProtocol.ParseRegistersFromReadResponse(response));
                }
            }
            catch (Exception ex)
            {
                LogHelper.Debug($"{Config.Name} periodic read exception: {ex.Message}");
            }
        }

        private void ApplyBitReadPlan(ModbusReadPlan readPlan, bool[] values)
        {
            foreach (var point in readPlan.Points)
            {
                int index = point.RegisterAddress - readPlan.StartAddress;
                if (index < 0 || index >= values.Length)
                {
                    continue;
                }

                ModbusRtuPointItem.SetPointValue(point, values[index] ? 1 : 0);
            }
        }

        private void ApplyRegisterReadPlan(ModbusReadPlan readPlan, ushort[] registers)
        {
            foreach (var point in readPlan.Points)
            {
                int startIndex = point.RegisterAddress - readPlan.StartAddress;
                TryConvertRegistersToPointValue(point, registers, startIndex);
            }
        }

        private List<ModbusReadPlan> BuildReadPlans(List<ModbusRtuPointItem> modbusPoints)
        {
            var readPlans = new List<ModbusReadPlan>();

            var activePoints = modbusPoints
                .Where(point => point.CanRead)
                .GroupBy(point => new
                {
                    point.RegisterType,
                    ReadInterval = GetEffectiveReadInterval(point)
                });

            foreach (var group in activePoints)
            {
                var orderedPoints = group
                    .OrderBy(point => point.RegisterAddress)
                    .ThenBy(point => point.BitOffset)
                    .ToList();

                ModbusReadPlan currentPlan = null;
                int maxQuantity = GetMaxReadQuantity(group.Key.RegisterType);
                bool allowReadSparseAddress = ModbusConfig == null || ModbusConfig.AllowReadSparseAddress;

                foreach (var point in orderedPoints)
                {
                    ushort pointStart = point.RegisterAddress;
                    ushort pointEnd = (ushort)(point.RegisterAddress + point.GetRegisterCount() - 1);

                    if (currentPlan == null)
                    {
                        currentPlan = CreateReadPlan(group.Key.RegisterType, group.Key.ReadInterval, pointStart, pointEnd, point);
                        continue;
                    }

                    ushort currentEnd = (ushort)(currentPlan.StartAddress + currentPlan.Quantity - 1);
                    ushort mergedEnd = Math.Max(currentEnd, pointEnd);
                    int mergedQuantity = mergedEnd - currentPlan.StartAddress + 1;
                    bool isContinuous = pointStart <= currentEnd + 1;
                    bool canMerge = (isContinuous || allowReadSparseAddress) && mergedQuantity <= maxQuantity;

                    if (canMerge)
                    {
                        currentPlan.Quantity = (ushort)mergedQuantity;
                        currentPlan.Points.Add(point);
                    }
                    else
                    {
                        readPlans.Add(currentPlan);
                        currentPlan = CreateReadPlan(group.Key.RegisterType, group.Key.ReadInterval, pointStart, pointEnd, point);
                    }
                }

                if (currentPlan != null)
                {
                    readPlans.Add(currentPlan);
                }
            }

            return readPlans;
        }

        private static ModbusReadPlan CreateReadPlan(ModbusRegisterType registerType, int readInterval, ushort pointStart, ushort pointEnd, ModbusRtuPointItem point)
        {
            var readPlan = new ModbusReadPlan
            {
                RegisterType = registerType,
                ReadInterval = readInterval,
                StartAddress = pointStart,
                Quantity = (ushort)(pointEnd - pointStart + 1)
            };
            readPlan.Points.Add(point);
            return readPlan;
        }

        private static int GetMaxReadQuantity(ModbusRegisterType registerType)
        {
            switch (registerType)
            {
                case ModbusRegisterType.Coil:
                case ModbusRegisterType.DiscreteInput:
                    return 2000;
                case ModbusRegisterType.HoldingRegister:
                case ModbusRegisterType.InputRegister:
                    return 125;
                default:
                    return 125;
            }
        }

        private int GetEffectiveReadInterval(ModbusRtuPointItem point)
        {
            if (point.ReadInterval > 0)
            {
                return point.ReadInterval;
            }

            if (ModbusConfig.ReadInterval > 0)
            {
                return ModbusConfig.ReadInterval;
            }

            return 1000;
        }

        private bool TryReadPointValue(ModbusRtuPointItem point, out object value, out string errorMessage)
        {
            value = null;
            errorMessage = string.Empty;

            ModbusRtuFrame request = ModbusRtuProtocol.BuildReadRequest(
                ModbusConfig.SlaveId,
                point.RegisterType,
                point.RegisterAddress,
                (ushort)point.GetRegisterCount());

            if (!TryExecuteRequest(request, out ModbusRtuFrame response, out errorMessage))
            {
                return false;
            }

            if (point.RegisterType == ModbusRegisterType.Coil || point.RegisterType == ModbusRegisterType.DiscreteInput)
            {
                bool[] bits = ModbusRtuProtocol.ParseBitsFromReadResponse(response);
                value = bits.Length > 0 && bits[0] ? 1 : 0;
                return true;
            }

            ushort[] registers = ModbusRtuProtocol.ParseRegistersFromReadResponse(response);
            return TryConvertRegistersToPointValue(point, registers, 0, out value);
        }

        private bool TryExecuteRequest(ModbusRtuFrame request, out ModbusRtuFrame response, out string errorMessage)
        {
            response = null;
            errorMessage = string.Empty;

            if (request == null)
            {
                errorMessage = "Request frame is null.";
                return false;
            }

            lock (_requestSyncLock)
            {
                if (!GetChannelIsConnected())
                {
                    errorMessage = "Serial port is not connected.";
                    return false;
                }

                byte[] requestBytes = request.ToBytes();
                byte[] responseBytes;
                long startTime = TimeHelper.GetNow();

                lock (_serialPortLock)
                {
                    SerialPort.DiscardInBuffer();
                    SerialPort.DiscardOutBuffer();
                    SerialPort.Write(requestBytes, 0, requestBytes.Length);
                }

                int interFrameDelay = Math.Max(ModbusConfig.InterFrameDelayMs, 0);
                if (interFrameDelay > 0)
                {
                    Thread.Sleep(interFrameDelay);
                }

                if (!TryReadResponseBytes(request, out responseBytes, out errorMessage))
                {
                    PrintFrameInfo("Modbus RTU error: " + errorMessage, requestBytes, responseBytes);
                    return false;
                }

                ReadTime = TimeHelper.GetNow() - startTime;
                if (!ModbusRtuFrame.TryParse(responseBytes, out response, out errorMessage))
                {
                    PrintFrameInfo("Modbus RTU parse error: " + errorMessage, requestBytes, responseBytes);
                    return false;
                }

                if (response.SlaveAddress != request.SlaveAddress)
                {
                    errorMessage = $"Slave address mismatch. Expected [{request.SlaveAddress}], actual [{response.SlaveAddress}].";
                    PrintFrameInfo("Modbus RTU error: " + errorMessage, requestBytes, responseBytes);
                    return false;
                }

                if (response.IsExceptionResponse)
                {
                    errorMessage = $"Modbus exception response. Function [{response.FunctionCode - 0x80}], code [{response.ExceptionCode}].";
                    PrintFrameInfo("Modbus RTU error: " + errorMessage, requestBytes, responseBytes);
                    return false;
                }

                if (response.FunctionCode != request.FunctionCode)
                {
                    errorMessage = $"Function code mismatch. Expected [{request.FunctionCode}], actual [{response.FunctionCode}].";
                    PrintFrameInfo("Modbus RTU error: " + errorMessage, requestBytes, responseBytes);
                    return false;
                }

                return true;
            }
        }

        private bool TryReadResponseBytes(ModbusRtuFrame request, out byte[] responseBytes, out string errorMessage)
        {
            responseBytes = null;
            errorMessage = string.Empty;

            int timeout = ModbusConfig.ResponseTimeout > 0 ? ModbusConfig.ResponseTimeout : 1000;
            var buffer = new List<byte>(256);

            if (!TryReadExact(buffer, 2, timeout, out errorMessage))
            {
                return false;
            }

            byte functionCode = buffer[1];
            if (functionCode == (byte)(request.FunctionCode | 0x80))
            {
                if (!TryReadExact(buffer, 5, timeout, out errorMessage))
                {
                    return false;
                }
            }
            else
            {
                switch (functionCode)
                {
                    case 0x01:
                    case 0x02:
                    case 0x03:
                    case 0x04:
                        if (!TryReadExact(buffer, 3, timeout, out errorMessage))
                        {
                            return false;
                        }

                        int byteCount = buffer[2];
                        if (!TryReadExact(buffer, 3 + byteCount + 2, timeout, out errorMessage))
                        {
                            return false;
                        }
                        break;
                    case 0x05:
                    case 0x06:
                    case 0x0F:
                    case 0x10:
                        if (!TryReadExact(buffer, 8, timeout, out errorMessage))
                        {
                            return false;
                        }
                        break;
                    default:
                        errorMessage = $"Unsupported function code [{functionCode}] in RTU response.";
                        return false;
                }
            }

            responseBytes = buffer.ToArray();
            return true;
        }

        private bool TryReadExact(List<byte> buffer, int targetLength, int timeoutMs, out string errorMessage)
        {
            errorMessage = string.Empty;
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (buffer.Count < targetLength)
            {
                if (!GetChannelIsConnected())
                {
                    errorMessage = "Serial port was closed while waiting for data.";
                    return false;
                }

                int bytesRead = 0;
                lock (_serialPortLock)
                {
                    int available = SerialPort.BytesToRead;
                    if (available > 0)
                    {
                        var chunk = new byte[Math.Min(available, targetLength - buffer.Count)];
                        bytesRead = SerialPort.Read(chunk, 0, chunk.Length);
                        for (int i = 0; i < bytesRead; i++)
                        {
                            buffer.Add(chunk[i]);
                        }
                    }
                }

                if (buffer.Count >= targetLength)
                {
                    break;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    errorMessage = $"Timed out while waiting for RTU response bytes. Target length [{targetLength}], actual [{buffer.Count}].";
                    return false;
                }

                if (bytesRead == 0)
                {
                    Thread.Sleep(2);
                }
            }

            return true;
        }

        private bool TryConvertRegistersToPointValue(ModbusRtuPointItem point, ushort[] registers, int startIndex)
        {
            if (!TryConvertRegistersToPointValue(point, registers, startIndex, out object value))
            {
                return false;
            }

            ModbusRtuPointItem.SetPointValue(point, value);
            return true;
        }

        private bool TryConvertRegistersToPointValue(ModbusRtuPointItem point, ushort[] registers, int startIndex, out object outValue)
        {
            outValue = null;

            if (point == null || registers == null || startIndex < 0)
            {
                return false;
            }

            int registerCount = point.GetRegisterCount();
            if (startIndex + registerCount > registers.Length)
            {
                return false;
            }

            switch (point.ValueType)
            {
                case ModbusValueType.Bit:
                    {
                        bool bitValue = (registers[startIndex] & (1 << point.BitOffset)) != 0;
                        outValue = bitValue ? 1 : 0;
                        return true;
                    }
                case ModbusValueType.UInt16:
                    outValue = Convert.ToInt32(registers[startIndex], CultureInfo.InvariantCulture);
                    return true;
                case ModbusValueType.Int16:
                    outValue = (int)(short)registers[startIndex];
                    return true;
                case ModbusValueType.UInt32:
                    {
                        byte[] bytes = RegistersToBigEndianBytes(registers, startIndex, 2);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(bytes);
                        }

                        outValue = (long)BitConverter.ToUInt32(bytes, 0);
                        return true;
                    }
                case ModbusValueType.Int32:
                    {
                        byte[] bytes = RegistersToBigEndianBytes(registers, startIndex, 2);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(bytes);
                        }

                        outValue = BitConverter.ToInt32(bytes, 0);
                        return true;
                    }
                case ModbusValueType.Float32:
                    {
                        byte[] bytes = RegistersToBigEndianBytes(registers, startIndex, 2);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(bytes);
                        }

                        outValue = (double)BitConverter.ToSingle(bytes, 0);
                        return true;
                    }
                case ModbusValueType.Float64:
                    {
                        byte[] bytes = RegistersToBigEndianBytes(registers, startIndex, 4);
                        if (BitConverter.IsLittleEndian)
                        {
                            Array.Reverse(bytes);
                        }

                        outValue = BitConverter.ToDouble(bytes, 0);
                        return true;
                    }
                default:
                    return false;
            }
        }

        private ushort[] BuildRegisterValuesForWrite<TValue>(ModbusRtuPointItem point, TValue inputValue, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                if (point.ValueType == ModbusValueType.Bit)
                {
                    if (!TryReadHoldingRegister(point.RegisterAddress, out ushort sourceValue, out errorMessage))
                    {
                        return null;
                    }

                    bool bitValue = ConvertToBool(inputValue);
                    if (bitValue)
                    {
                        sourceValue = (ushort)(sourceValue | (1 << point.BitOffset));
                    }
                    else
                    {
                        sourceValue = (ushort)(sourceValue & ~(1 << point.BitOffset));
                    }

                    return new[] { sourceValue };
                }

                switch (point.ValueType)
                {
                    case ModbusValueType.UInt16:
                        return new[] { Convert.ToUInt16(inputValue, CultureInfo.InvariantCulture) };
                    case ModbusValueType.Int16:
                        return new[] { unchecked((ushort)Convert.ToInt16(inputValue, CultureInfo.InvariantCulture)) };
                    case ModbusValueType.UInt32:
                        return BigEndianBytesToRegisters(ToBigEndianBytes(ConvertToUInt32(inputValue)));
                    case ModbusValueType.Int32:
                        return BigEndianBytesToRegisters(ToBigEndianBytes(Convert.ToInt32(inputValue, CultureInfo.InvariantCulture)));
                    case ModbusValueType.Float32:
                        return BigEndianBytesToRegisters(ToBigEndianBytes(Convert.ToSingle(inputValue, CultureInfo.InvariantCulture)));
                    case ModbusValueType.Float64:
                        return BigEndianBytesToRegisters(ToBigEndianBytes(Convert.ToDouble(inputValue, CultureInfo.InvariantCulture)));
                    default:
                        errorMessage = $"Unsupported write value type [{point.ValueType}].";
                        return null;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return null;
            }
        }

        private bool TryReadHoldingRegister(ushort registerAddress, out ushort registerValue, out string errorMessage)
        {
            registerValue = 0;
            errorMessage = string.Empty;

            ModbusRtuFrame request = ModbusRtuProtocol.BuildReadRequest(
                ModbusConfig.SlaveId,
                ModbusRegisterType.HoldingRegister,
                registerAddress,
                1);

            if (!TryExecuteRequest(request, out ModbusRtuFrame response, out errorMessage))
            {
                return false;
            }

            ushort[] registers = ModbusRtuProtocol.ParseRegistersFromReadResponse(response);
            if (registers.Length == 0)
            {
                errorMessage = "Holding register read returned no data.";
                return false;
            }

            registerValue = registers[0];
            return true;
        }

        private static bool ConvertToBool<TValue>(TValue inputValue)
        {
            if (inputValue is bool boolValue)
            {
                return boolValue;
            }

            if (inputValue is string text)
            {
                if (bool.TryParse(text, out bool parsedBool))
                {
                    return parsedBool;
                }

                if (int.TryParse(text, out int parsedInt))
                {
                    return parsedInt != 0;
                }
            }

            return Convert.ToInt32(inputValue, CultureInfo.InvariantCulture) != 0;
        }

        private static uint ConvertToUInt32<TValue>(TValue inputValue)
        {
            if (inputValue is uint uintValue)
            {
                return uintValue;
            }

            if (inputValue is long longValue)
            {
                return checked((uint)longValue);
            }

            if (inputValue is ulong ulongValue)
            {
                return checked((uint)ulongValue);
            }

            return Convert.ToUInt32(inputValue, CultureInfo.InvariantCulture);
        }

        private static byte[] RegistersToBigEndianBytes(ushort[] registers, int startIndex, int registerCount)
        {
            var bytes = new byte[registerCount * 2];
            for (int i = 0; i < registerCount; i++)
            {
                ushort registerValue = registers[startIndex + i];
                bytes[i * 2] = (byte)(registerValue >> 8);
                bytes[i * 2 + 1] = (byte)(registerValue & 0xFF);
            }

            return bytes;
        }

        private static ushort[] BigEndianBytesToRegisters(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length % 2 != 0)
            {
                return Array.Empty<ushort>();
            }

            var registers = new ushort[bytes.Length / 2];
            for (int i = 0; i < registers.Length; i++)
            {
                registers[i] = (ushort)((bytes[i * 2] << 8) | bytes[i * 2 + 1]);
            }

            return registers;
        }

        private static byte[] ToBigEndianBytes(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] ToBigEndianBytes(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] ToBigEndianBytes(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static byte[] ToBigEndianBytes(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private void PrintFrameInfo(string prefix, byte[] sendFrame, byte[] responseFrame)
        {
            LogHelper.Debug($"{prefix} send:[{(sendFrame == null ? string.Empty : BitConverter.ToString(sendFrame))}] response:[{(responseFrame == null ? string.Empty : BitConverter.ToString(responseFrame))}]");
        }
    }
}
