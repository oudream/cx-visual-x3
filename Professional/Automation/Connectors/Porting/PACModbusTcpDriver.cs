// PORTING REVIEW: ReferTo source copied and type-renamed; NOT INCLUDED IN PROJECT.
// Original: ReferTo/Protocols/ModbusTcpModule/ModbusTcpDriver.cs; requires dependency cleanup, no compatibility Adapter will be retained.
using CommonInterfaces;
using CxVisual.Platform.Channels;
using CxVisual.Platform.VariableObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

namespace CxVisual.Professional.Automation.Porting
{
    public class PACModbusTcpDriver : DriverModuleBase
    {
        private sealed class PendingRequest
        {
            public PendingRequest(ushort transactionId)
            {
                TransactionId = transactionId;
            }

            public ushort TransactionId { get; }

            public ManualResetEventSlim CompletedEvent { get; } = new ManualResetEventSlim(false);

            public MPAModbusTcpFrame Response { get; private set; }

            public string ErrorMessage { get; private set; }

            public void SetResponse(MPAModbusTcpFrame response)
            {
                Response = response;
                CompletedEvent.Set();
            }

            public void SetError(string errorMessage)
            {
                ErrorMessage = errorMessage;
                CompletedEvent.Set();
            }
        }

        private sealed class ModbusReadPlan
        {
            public ModbusRegisterType RegisterType { get; set; }

            public int ReadInterval { get; set; }

            public ushort StartAddress { get; set; }

            public ushort Quantity { get; set; }

            public long LastReadTime { get; set; }

            public List<ModbusTcpPointItem> Points { get; } = new List<ModbusTcpPointItem>();
        }

        public enum ModbusModuleState
        {
            Stopping,
            Stopped,
            Started
        }

        [Browsable(false)]
        public TcpClientChannel TcpClientChannel { get; private set; }

        [Browsable(false)]
        public ModbusTcpDriverModuleConfig ModbusConfig { get; private set; }

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

        // 同一条 Modbus TCP 连接上串行发请求，避免并发请求导致响应错配。
        private readonly object _requestSyncLock = new object();
        private readonly object _pendingRequestLock = new object();
        private readonly object _receiveBufferLock = new object();
        private readonly List<byte> _receiveBuffer = new List<byte>(1024);

        private Thread _readingThread;
        private volatile bool _isReading;
        private PendingRequest _pendingRequest;
        private List<ModbusReadPlan> _readPlans = new List<ModbusReadPlan>();
        private ushort _transactionId;

        public long ReadTime = 0;

        public PACModbusTcpDriver(DriverModuleConfig config) : base(config)
        {
            ModbusConfig = config as ModbusTcpDriverModuleConfig;
            if (ModbusConfig == null)
            {
                throw new ArgumentException($"{config.Name} config Not Is ModbusTcpDriverModuleConfig");
            }
        }

        public override void StartReadDynamicProperty()
        {
            if (ModuleState != ModbusModuleState.Stopped)
            {
                return;
            }

            if (!IPAddress.TryParse(ModbusConfig.RemoteIp, out _))
            {
                LogHelper.Error($"{Config.Name} 无效的 Modbus TCP IP 地址：{ModbusConfig.RemoteIp}");
                return;
            }

            var modbusPoints = GetModbusPoints();
            CheckModbusPoints(modbusPoints);
            _readPlans = BuildReadPlans(modbusPoints);

            EnsureChannel();

            if (TcpClientChannel.ModuleState == TcpClientModuleState.Stopped)
            {
                TcpClientChannel.Start(ModbusConfig.RemoteIp, ModbusConfig.RemotePort);
            }

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
            CancelPendingRequest("驱动停止");

            if (TcpClientChannel != null &&
                (TcpClientChannel.ModuleState == TcpClientModuleState.Started || TcpClientChannel.ModuleState == TcpClientModuleState.Connected))
            {
                TcpClientChannel.Stop();
            }

            _readingThread?.Join();
            _readingThread = null;

            lock (_receiveBufferLock)
            {
                _receiveBuffer.Clear();
            }

            ModuleState = ModbusModuleState.Stopped;
        }

        public bool GetChannelIsConnected()
        {
            return TcpClientChannel != null && TcpClientChannel.IsConnected();
        }

        public List<ModbusTcpPointItem> GetModbusPoints()
        {
            var result = new List<ModbusTcpPointItem>();
            foreach (var point in PointSheet.Points)
            {
                if (point is ModbusTcpPointItem modbusPoint)
                {
                    result.Add(modbusPoint);
                }
            }
            return result;
        }

        protected override bool PointFromContextImpl(Dictionary<string, string> context, PointItem pointItem)
        {
            if (pointItem is ModbusTcpPointItem modbusPoint)
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
            if (pointItem is ModbusTcpPointItem modbusPoint)
            {
                context["RegisterType"] = modbusPoint.RegisterType.ToString();
                context["ValueType"] = modbusPoint.ValueType.ToString();
                context["Address"] = modbusPoint.Address;
                context["ReadInterval"] = modbusPoint.ReadInterval.ToString();
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
                    ModbusTcpPointItem.CreateAndRegisterDevice(point.DeviceName, point.ValueType);
                }
            }
        }

        public override PointItem CreatePointItem(string deviceName, string variableName)
        {
            return new ModbusTcpPointItem(deviceName, variableName);
        }

        protected override SendPointValueResult DoSendPointValue<TValue>(PointItem pointItem, TValue value)
        {
            if (!(pointItem is ModbusTcpPointItem modbusPoint))
            {
                return SendPointValueResult.InternalError;
            }

            if (!GetChannelIsConnected())
            {
                return SendPointValueResult.SendFailed;
            }

            try
            {
                MPAModbusTcpFrame request;
                if (modbusPoint.RegisterType == ModbusRegisterType.Coil)
                {
                    bool coilValue = ConvertToBool(value);
                    request = ModbusTcpProtocol.BuildWriteSingleCoilRequest(ModbusConfig.SlaveId, modbusPoint.RegisterAddress, coilValue);
                }
                else if (modbusPoint.RegisterType == ModbusRegisterType.HoldingRegister)
                {
                    ushort[] registers = BuildRegisterValuesForWrite(modbusPoint, value, out string buildError);
                    if (registers == null)
                    {
                        LogHelper.Error($"{Config.Name} 写入点[{modbusPoint.DeviceName}.{modbusPoint.VariableName}]失败：{buildError}");
                        return SendPointValueResult.InternalError;
                    }

                    request = registers.Length == 1
                        ? ModbusTcpProtocol.BuildWriteSingleRegisterRequest(ModbusConfig.SlaveId, modbusPoint.RegisterAddress, registers[0])
                        : ModbusTcpProtocol.BuildWriteMultipleRegistersRequest(ModbusConfig.SlaveId, modbusPoint.RegisterAddress, registers);
                }
                else
                {
                    LogHelper.Error($"{Config.Name} 点[{modbusPoint.DeviceName}.{modbusPoint.VariableName}]当前寄存器区不支持写入");
                    return SendPointValueResult.InternalError;
                }

                if (!TryExecuteRequest(request, out MPAModbusTcpFrame response, out string errorMessage))
                {
                    LogHelper.Error($"{Config.Name} 写入点[{modbusPoint.DeviceName}.{modbusPoint.VariableName}]失败：{errorMessage}");
                    return SendPointValueResult.SendFailed;
                }

                if (response == null)
                {
                    return SendPointValueResult.SendFailed;
                }

                return SendPointValueResult.SentAndAcknowledged;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"{Config.Name} 写入点[{modbusPoint.DeviceName}.{modbusPoint.VariableName}]异常：{ex.Message}");
                return SendPointValueResult.SendFailed;
            }
        }

        protected override (SendPointValueResult, TValue) DoReadPointValue<TValue>(PointItem pointItem, TValue defalutValue)
        {
            if (!(pointItem is ModbusTcpPointItem modbusPoint))
            {
                return (SendPointValueResult.InternalError, defalutValue);
            }

            if (TryReadPointValue(modbusPoint, out object pointValue, out string errorMessage))
            {
                try
                {
                    if (pointValue is TValue value)
                    {
                        return (SendPointValueResult.SentAndAcknowledged, value);
                    }

                    TValue convertedValue = (TValue)Convert.ChangeType(pointValue, typeof(TValue), CultureInfo.InvariantCulture);
                    return (SendPointValueResult.SentAndAcknowledged, convertedValue);
                }
                catch
                {
                    return (SendPointValueResult.SentAndAcknowledged, defalutValue);
                }
            }

            LogHelper.Error($"{Config.Name} 读取点[{modbusPoint.DeviceName}.{modbusPoint.VariableName}]失败：{errorMessage}");
            return (SendPointValueResult.SendFailed, defalutValue);
        }

        [Obfuscation(Exclude = true)]
        public void CheckModbusPoints(List<ModbusTcpPointItem> modbusPoints)
        {
            var duplicates = modbusPoints
                .GroupBy(item => new { item.RegisterType, item.RegisterAddress, item.BitOffset, item.ValueType })
                .Where(group => group.Count() > 1)
                .SelectMany(group => group.Skip(1))
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new ArgumentException($"{Config.Name} 点表中存在重复的寄存器定义");
            }

            foreach (var point in modbusPoints)
            {
                string checkResult = ModbusTcpPointItem.CheckModbusPoint(point);
                if (!string.IsNullOrEmpty(checkResult))
                {
                    throw new ArgumentException($"{Config.Name} 点[{point.DeviceName}]检查失败：{checkResult}");
                }
            }
        }

        public static void RegisterFactory()
        {
            DriverModuleFactoryCenter.RegisterFactory(
                StringHelper.ExtractShortNameAfterDot(typeof(PACModbusTcpDriver).Name),
                "Modbus TCP驱动模块",
                driverModuleConfig => new PACModbusTcpDriver(driverModuleConfig));
        }

        private void EnsureChannel()
        {
            if (TcpClientChannel != null)
            {
                return;
            }

            TcpClientChannel = new TcpClientChannel(ModbusConfig.LogEnabled);
            TcpClientChannel.DataReceivedEvent += TcpClientChannel_DataReceivedEvent;
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
                MPAModbusTcpFrame request = ModbusTcpProtocol.BuildReadRequest(
                    ModbusConfig.SlaveId,
                    readPlan.RegisterType,
                    readPlan.StartAddress,
                    readPlan.Quantity);

                if (!TryExecuteRequest(request, out MPAModbusTcpFrame response, out string errorMessage))
                {
                    LogHelper.Debug($"{Config.Name} 定时读取[{readPlan.RegisterType}] {readPlan.StartAddress}-{readPlan.StartAddress + readPlan.Quantity - 1} 失败：{errorMessage}");
                    return;
                }

                if (readPlan.RegisterType == ModbusRegisterType.Coil || readPlan.RegisterType == ModbusRegisterType.DiscreteInput)
                {
                    ApplyBitReadPlan(readPlan, ModbusTcpProtocol.ParseBitsFromReadResponse(response));
                }
                else
                {
                    ApplyRegisterReadPlan(readPlan, ModbusTcpProtocol.ParseRegistersFromReadResponse(response));
                }
            }
            catch (Exception ex)
            {
                LogHelper.Debug($"{Config.Name} 定时读取异常：{ex.Message}");
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

                ModbusTcpPointItem.SetPointValue(point, values[index] ? 1 : 0);
            }
        }

        private void ApplyRegisterReadPlan(ModbusReadPlan readPlan, ushort[] registers)
        {
            foreach (var point in readPlan.Points)
            {
                int startIndex = point.RegisterAddress - readPlan.StartAddress;
                if (!TryConvertRegistersToPointValue(point, registers, startIndex))
                {
                    continue;
                }
            }
        }

        private List<ModbusReadPlan> BuildReadPlans(List<ModbusTcpPointItem> modbusPoints)
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

                    // 允许读空洞地址时，稀疏点位也尽量合并，减少 Modbus 请求次数。
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

        private static ModbusReadPlan CreateReadPlan(ModbusRegisterType registerType, int readInterval, ushort pointStart, ushort pointEnd, ModbusTcpPointItem point)
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

        private int GetEffectiveReadInterval(ModbusTcpPointItem point)
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

        private bool TryReadPointValue(ModbusTcpPointItem point, out object value, out string errorMessage)
        {
            value = null;
            errorMessage = string.Empty;

            MPAModbusTcpFrame request = ModbusTcpProtocol.BuildReadRequest(
                ModbusConfig.SlaveId,
                point.RegisterType,
                point.RegisterAddress,
                (ushort)point.GetRegisterCount());

            if (!TryExecuteRequest(request, out MPAModbusTcpFrame response, out errorMessage))
            {
                return false;
            }

            if (point.RegisterType == ModbusRegisterType.Coil || point.RegisterType == ModbusRegisterType.DiscreteInput)
            {
                bool[] bits = ModbusTcpProtocol.ParseBitsFromReadResponse(response);
                value = bits.Length > 0 && bits[0] ? 1 : 0;
                return true;
            }

            ushort[] registers = ModbusTcpProtocol.ParseRegistersFromReadResponse(response);
            return TryConvertRegistersToPointValue(point, registers, 0, out value);
        }

        private bool TryExecuteRequest(MPAModbusTcpFrame request, out MPAModbusTcpFrame response, out string errorMessage)
        {
            response = null;
            errorMessage = string.Empty;

            if (request == null)
            {
                errorMessage = "请求帧为空";
                return false;
            }

            lock (_requestSyncLock)
            {
                if (!GetChannelIsConnected())
                {
                    errorMessage = "TCP通道未连接";
                    return false;
                }

                request.TransactionId = NextTransactionId();
                var pendingRequest = new PendingRequest(request.TransactionId);

                lock (_pendingRequestLock)
                {
                    _pendingRequest = pendingRequest;
                }

                long startTime = TimeHelper.GetNow();
                bool sendResult = TcpClientChannel.Send(request.ToBytes(), loggingEnabled: ModbusConfig.LogEnabled, logSTR: false);
                if (!sendResult)
                {
                    ClearPendingRequest(pendingRequest);
                    errorMessage = "请求发送失败";
                    return false;
                }

                int timeout = ModbusConfig.ResponseTimeout > 0 ? ModbusConfig.ResponseTimeout : 1000;
                if (!pendingRequest.CompletedEvent.Wait(timeout))
                {
                    ClearPendingRequest(pendingRequest);
                    errorMessage = $"等待响应超时[{timeout}]ms";
                    TcpClientChannel.PushClose();
                    return false;
                }

                ClearPendingRequest(pendingRequest);
                ReadTime = TimeHelper.GetNow() - startTime;

                if (!string.IsNullOrEmpty(pendingRequest.ErrorMessage))
                {
                    errorMessage = pendingRequest.ErrorMessage;
                    PrintFrameInfo("ModbusTcp出现异常:"+pendingRequest.ErrorMessage, request, pendingRequest.Response);
                    return false;
                }

                response = pendingRequest.Response;
                if (response == null)
                {
                    errorMessage = "响应帧为空";
                    PrintFrameInfo("ModbusTcp出现异常:"+ errorMessage, request, pendingRequest.Response);
                    return false;
                }

                if (response.UnitId != request.UnitId)
                {
                    errorMessage = $"响应站号不匹配，期望[{request.UnitId}]，实际[{response.UnitId}]";
                    PrintFrameInfo("ModbusTcp出现异常:" + errorMessage, request, pendingRequest.Response);
                    return false;
                }

                if (response.FunctionCode != request.FunctionCode)
                {
                    errorMessage = $"响应功能码不匹配，期望[{request.FunctionCode}]，实际[{response.FunctionCode}]";
                    PrintFrameInfo("ModbusTcp出现异常:" + errorMessage, request, pendingRequest.Response);
                    return false;
                }

                return response != null;
            }
        }

        private void TcpClientChannel_DataReceivedEvent(byte[] buffer, int bytesRead)
        {
            if (bytesRead <= 0)
            {
                return;
            }

            byte[] dataCopy = new byte[bytesRead];
            Array.Copy(buffer, dataCopy, bytesRead);

            lock (_receiveBufferLock)
            {
                _receiveBuffer.AddRange(dataCopy);

                while (MPAModbusTcpFrame.TryParse(_receiveBuffer, out MPAModbusTcpFrame frame, out int frameLength))
                {
                    _receiveBuffer.RemoveRange(0, frameLength);
                    ProcessFrame(frame);
                    if (_receiveBuffer.Count == 0)
                    {
                        break;
                    }
                }
            }
        }

        private void ProcessFrame(MPAModbusTcpFrame frame)
        {
            PendingRequest pendingRequest = null;
            lock (_pendingRequestLock)
            {
                if (_pendingRequest != null && _pendingRequest.TransactionId == frame.TransactionId)
                {
                    pendingRequest = _pendingRequest;
                }
            }

            // 先把报文交给当前等待中的同步请求；如果没有等待者，再按被动接收报文处理。
            if (pendingRequest != null)
            {
                if (frame.IsExceptionResponse)
                {
                    pendingRequest.SetError($"Modbus异常响应：功能码[{frame.FunctionCode - 0x80}] 异常码[{frame.ExceptionCode}]");
                }
                else
                {
                    pendingRequest.SetResponse(frame);
                }
                return;
            }

            TryApplyPassiveFrame(frame);
        }

        private void TryApplyPassiveFrame(MPAModbusTcpFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            if (ModbusTcpProtocol.TryParsePassiveWriteRegisters(frame, out ushort registerStartAddress, out ushort[] registerValues))
            {
                ApplyPassiveRegisterValues(registerStartAddress, registerValues);
                return;
            }

            if (ModbusTcpProtocol.TryParsePassiveWriteCoils(frame, out ushort coilStartAddress, out bool[] coilValues))
            {
                ApplyPassiveCoilValues(coilStartAddress, coilValues);
            }
        }

        private void ApplyPassiveRegisterValues(ushort startAddress, ushort[] registerValues)
        {
            foreach (var point in GetModbusPoints().Where(point => point.RegisterType == ModbusRegisterType.HoldingRegister))
            {
                int startIndex = point.RegisterAddress - startAddress;
                if (!TryConvertRegistersToPointValue(point, registerValues, startIndex))
                {
                    continue;
                }
            }
        }

        private void ApplyPassiveCoilValues(ushort startAddress, bool[] coilValues)
        {
            foreach (var point in GetModbusPoints().Where(point => point.RegisterType == ModbusRegisterType.Coil))
            {
                int index = point.RegisterAddress - startAddress;
                if (index < 0 || index >= coilValues.Length)
                {
                    continue;
                }

                ModbusTcpPointItem.SetPointValue(point, coilValues[index] ? 1 : 0);
            }
        }

        private void CancelPendingRequest(string errorMessage)
        {
            lock (_pendingRequestLock)
            {
                _pendingRequest?.SetError(errorMessage);
                _pendingRequest = null;
            }
        }

        private void ClearPendingRequest(PendingRequest pendingRequest)
        {
            lock (_pendingRequestLock)
            {
                if (ReferenceEquals(_pendingRequest, pendingRequest))
                {
                    _pendingRequest = null;
                }
            }
            pendingRequest.CompletedEvent.Dispose();
        }

        private ushort NextTransactionId()
        {
            unchecked
            {
                _transactionId++;
                if (_transactionId == 0)
                {
                    _transactionId = 1;
                }
            }

            return _transactionId;
        }

        private bool TryConvertRegistersToPointValue(ModbusTcpPointItem point, ushort[] registers, int startIndex)
        {
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
                        int value = bitValue ? 1 : 0;
                        ModbusTcpPointItem.SetPointValue(point, value);
                        return true;
                    }
                case ModbusValueType.UInt16:
                    {
                        int value = Convert.ToInt32(registers[startIndex]);
                        ModbusTcpPointItem.SetPointValue(point, value);
                        return true;
                    }
                case ModbusValueType.Int16:
                    {
                        int value = (int)(short)registers[startIndex];
                        ModbusTcpPointItem.SetPointValue(point, value);
                        return true;
                    }
                case ModbusValueType.UInt32:
                    {
                        byte[] bytes = BuildMachineBytesForRead(registers, startIndex, 2);
                        long value = (long)BitConverter.ToUInt32(bytes, 0);
                        ModbusTcpPointItem.SetPointValue(point, value);
                        return true;
                    }
                case ModbusValueType.Int32:
                    {
                        byte[] bytes = BuildMachineBytesForRead(registers, startIndex, 2);
                        int value = BitConverter.ToInt32(bytes, 0);
                        ModbusTcpPointItem.SetPointValue(point, value);
                        return true;
                    }
                case ModbusValueType.Float32:
                    {
                        byte[] bytes = BuildMachineBytesForRead(registers, startIndex, 2);
                        double value = (double)BitConverter.ToSingle(bytes, 0);
                        ModbusTcpPointItem.SetPointValue(point, value);
                        return true;
                    }
                case ModbusValueType.Float64:
                    {
                        byte[] bytes = BuildMachineBytesForRead(registers, startIndex, 4);
                        double value = BitConverter.ToDouble(bytes, 0);
                        ModbusTcpPointItem.SetPointValue(point, value);
                        return true;
                    }
                default:
                    return false;
            }
        }

        private bool TryConvertRegistersToPointValue(ModbusTcpPointItem point, ushort[] registers, int startIndex, out object outValue)
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
                        int value = bitValue ? 1 : 0;
                        outValue = value;
                        return true;
                    }
                case ModbusValueType.UInt16:
                    {
                        int value = Convert.ToInt32(registers[startIndex]);
                        outValue = value;
                        return true;
                    }
                case ModbusValueType.Int16:
                    {
                        int value = (int)(short)registers[startIndex];
                        outValue = value;
                        return true;
                    }
                case ModbusValueType.UInt32:
                    {
                        byte[] bytes = BuildMachineBytesForRead(registers, startIndex, 2);
                        long value = (long)BitConverter.ToUInt32(bytes, 0);
                        outValue = value;
                        return true;
                    }
                case ModbusValueType.Int32:
                    {
                        byte[] bytes = BuildMachineBytesForRead(registers, startIndex, 2);
                        int value = BitConverter.ToInt32(bytes, 0);
                        outValue = value;
                        return true;
                    }
                case ModbusValueType.Float32:
                    {
                        byte[] bytes = BuildMachineBytesForRead(registers, startIndex, 2);
                        double value = (double)BitConverter.ToSingle(bytes, 0);
                        outValue = value;
                        return true;
                    }
                case ModbusValueType.Float64:
                    {
                        byte[] bytes = BuildMachineBytesForRead(registers, startIndex, 4);
                        double value = BitConverter.ToDouble(bytes, 0);
                        outValue = value;
                        return true;
                    }
                default:
                    return false;
            }
        }

        private ushort[] BuildRegisterValuesForWrite<TValue>(ModbusTcpPointItem point, TValue inputValue, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                if (point.ValueType == ModbusValueType.Bit)
                {
                    // 保持寄存器的位写入需要先读后改，避免把同一个寄存器里的其他位覆盖掉。
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
                        {
                            uint value = ConvertToUInt32(inputValue);
                            return BuildRegisterValuesForWrite(ToBigEndianBytes(value));
                        }
                    case ModbusValueType.Int32:
                        {
                            int value = Convert.ToInt32(inputValue, CultureInfo.InvariantCulture);
                            return BuildRegisterValuesForWrite(ToBigEndianBytes(value));
                        }
                    case ModbusValueType.Float32:
                        {
                            float value = Convert.ToSingle(inputValue, CultureInfo.InvariantCulture);
                            return BuildRegisterValuesForWrite(ToBigEndianBytes(value));
                        }
                    case ModbusValueType.Float64:
                        {
                            double value = Convert.ToDouble(inputValue, CultureInfo.InvariantCulture);
                            return BuildRegisterValuesForWrite(ToBigEndianBytes(value));
                        }
                    default:
                        errorMessage = $"不支持的写入值类型[{point.ValueType}]";
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

            MPAModbusTcpFrame request = ModbusTcpProtocol.BuildReadRequest(
                ModbusConfig.SlaveId,
                ModbusRegisterType.HoldingRegister,
                registerAddress,
                1);

            if (!TryExecuteRequest(request, out MPAModbusTcpFrame response, out errorMessage))
            {
                return false;
            }

            ushort[] registers = ModbusTcpProtocol.ParseRegistersFromReadResponse(response);
            if (registers.Length == 0)
            {
                errorMessage = "读取保持寄存器返回为空";
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

        // Modbus 返回的寄存器天然只有“每个寄存器高字节在前”，多寄存器值的整体排列顺序需要再按配置重排。
        private byte[] BuildMachineBytesForRead(ushort[] registers, int startIndex, int registerCount)
        {
            byte[] registerBytes = RegistersToRegisterBytes(registers, startIndex, registerCount);
            byte[] canonicalBytes = ReorderRegisterBytesForRead(registerBytes, registerCount, GetConfiguredByteOrder());

            if (BitConverter.IsLittleEndian)
            { 
                Array.Reverse(canonicalBytes);
            }

            return canonicalBytes;
        }

        // 写入时先按 .NET 数值得到规范字节序，再反向排成 PLC 当前要求的寄存器顺序。
        private ushort[] BuildRegisterValuesForWrite(byte[] canonicalBytes)
        {
            byte[] registerBytes = ReorderRegisterBytesForWrite(canonicalBytes, GetConfiguredByteOrder());
            return RegisterBytesToRegisters(registerBytes);
        }

        private ModbusMultiRegisterByteOrder GetConfiguredByteOrder()
        {
            return ModbusConfig == null
                ? ModbusMultiRegisterByteOrder.AbCd
                : ModbusConfig.MultiRegisterByteOrder;
        }

        private static byte[] RegistersToRegisterBytes(ushort[] registers, int startIndex, int registerCount)
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

        private static ushort[] RegisterBytesToRegisters(byte[] bytes)
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

        private static byte[] ReorderRegisterBytesForRead(byte[] registerBytes, int registerCount, ModbusMultiRegisterByteOrder byteOrder)
        {
            int[] readMap = GetReadMap(registerCount, byteOrder);
            if (readMap == null)
            {
                return (byte[])registerBytes.Clone();
            }

            var canonicalBytes = new byte[registerBytes.Length];
            for (int i = 0; i < canonicalBytes.Length; i++)
            {
                canonicalBytes[i] = registerBytes[readMap[i]];
            }

            return canonicalBytes;
        }

        private static byte[] ReorderRegisterBytesForWrite(byte[] canonicalBytes, ModbusMultiRegisterByteOrder byteOrder)
        {
            int registerCount = canonicalBytes == null ? 0 : canonicalBytes.Length / 2;
            int[] readMap = GetReadMap(registerCount, byteOrder);
            if (canonicalBytes == null || readMap == null)
            {
                return canonicalBytes == null ? Array.Empty<byte>() : (byte[])canonicalBytes.Clone();
            }

            var registerBytes = new byte[canonicalBytes.Length];
            for (int i = 0; i < canonicalBytes.Length; i++)
            {
                registerBytes[readMap[i]] = canonicalBytes[i];
            }

            return registerBytes;
        }

        private static int[] GetReadMap(int registerCount, ModbusMultiRegisterByteOrder byteOrder)
        {
            if (registerCount == 2)
            {
                switch (byteOrder)
                {
                    case ModbusMultiRegisterByteOrder.AbCd:
                        return new[] { 0, 1, 2, 3 };
                    case ModbusMultiRegisterByteOrder.CdAb:
                        return new[] { 2, 3, 0, 1 };
                    case ModbusMultiRegisterByteOrder.BaDc:
                        return new[] { 1, 0, 3, 2 };
                    case ModbusMultiRegisterByteOrder.DcBa:
                        return new[] { 3, 2, 1, 0 };
                }
            }

            if (registerCount == 4)
            {
                switch (byteOrder)
                {
                    case ModbusMultiRegisterByteOrder.AbCd:
                        return new[] { 0, 1, 2, 3, 4, 5, 6, 7 };
                    case ModbusMultiRegisterByteOrder.CdAb:
                        return new[] { 6, 7, 4, 5, 2, 3, 0, 1 };
                    case ModbusMultiRegisterByteOrder.BaDc:
                        return new[] { 1, 0, 3, 2, 5, 4, 7, 6 };
                    case ModbusMultiRegisterByteOrder.DcBa:
                        return new[] { 7, 6, 5, 4, 3, 2, 1, 0 };
                }
            }

            return null;
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
        private void PrintFrameInfo(string PrefixString, MPAModbusTcpFrame SendFrame, MPAModbusTcpFrame ResponseFrame)
        {
            LogHelper.Debug($"{PrefixString}----ModbusTcp发送Frame:{BitConverter.ToString(SendFrame.ToBytes())}" +
                $"----接收Frame:{(ResponseFrame == null ? "未能成功接收帧" : BitConverter.ToString(ResponseFrame.ToBytes()))}"
);
        }
    }
}
