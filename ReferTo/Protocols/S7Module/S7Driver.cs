using CommonInterfaces;
using CxVisual.Platform.VariableObjects;
using S7.Net;
using S7.Net.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace CxVisual.Platform.Protocols.S7
{
    public class S7Driver : DriverModuleBase
    {
        public struct DBAreaInfo
        {
            public int DB;
            public int StartByteAdr;
            public int Count;
        }

        public enum S7ModuleState
        {
            Stopping, 
            Stopped,
            Started
        }

        [Browsable(false)]
        public Plc plc;

        [Browsable(false)]
        public S7DriverModuleConfig S7Config { get; private set; }

        // S7驱动模板状态变更事件
        public event EventHandler<S7ModuleState> ModuleStateChanged;
        private volatile S7ModuleState _moduleState = S7ModuleState.Stopped;
        public S7ModuleState ModuleState
        {
            get => _moduleState;
            private set
            {
                if (_moduleState != value)
                {
                    //LogHelper.Debug($"{Config.Name} {(value ? "Start" : "Stop")}");
                    _moduleState = value;
                    OnRunningStateChanged(_moduleState);
                }
            }
        }
        protected virtual void OnRunningStateChanged(S7ModuleState moduleState)
        {
            // ?. 表示如果 RunningStateChanged 不为 null，则调用 Invoke
            ModuleStateChanged?.Invoke(this, moduleState);
        }

        private readonly object plcLock = new object(); // 确保对 PLC 操作的线程安全
        private Thread channelThread;
        private Thread readingThread;

        public S7Driver(DriverModuleConfig config) : base(config)
        {
            S7Config = config as S7DriverModuleConfig;
            if (S7Config == null)
            {
                throw new ArgumentException($"{config.Name} config Not Is S7DriverModuleConfig");
            }
        }

        private volatile bool isStarting = false;
        private volatile bool isReading = false;
        //long currentValue = Interlocked.Read(ref BusinessHelper.S7DriverOne.ReadTime);
        public long ReadTime = 0;
        private bool plcError = false;

        // 启动读取模块，启动PLC通道
        public override void StartReadDynamicProperty()
        {
            // 检查是否已停止
            if (ModuleState != S7ModuleState.Stopped)
            {
                return;
            }

            StartChannelImpl();
            StartReadDynamicPropertyImpl();

            ModuleState = S7ModuleState.Started;
        }

        // 启动单独线程来维护PLC通道
        private void StartChannelImpl()
        {
            if (isStarting)
            {
                return;
            }

            LogHelper.Debug($"{Config.Name} PLC通道维护模块已启动");
            isStarting = true;

            // 创建PLC、关闭已存在的PLC、重连PLC
            channelThread = new Thread(() =>
            {
                while (isStarting)
                {
                    if (!NetHelper.ValidateIPv4(S7Config.PlcIp))
                    {
                        LogHelper.Error($"{Config.Name} 无效的PLC IP地址：{S7Config.PlcIp}");
                        continue;
                    }

                    lock (plcLock)
                    {
                        // 关闭已存在问题的PLC
                        if (plcError)
                        {
                            if (plc != null)
                            {
                                plc.Close();
                                plc = null;
                            }
                            plcError = false;
                        }
                        // 创建PLC
                        if (plc == null)
                        {
                            // rack: PLC的机架，通常是0，但需在Step7或TIA门户的硬件配置中检查
                            // slot: PLC的CPU插槽，通常对于S7300-S7400是2，对于S7-1200和S7-1500是0。如果使用外部以太网卡，则必须相应设置。
                            Int16 slot = (short)(S7Config.CpuType == CpuType.S71200 || S7Config.CpuType == CpuType.S71500 ? 0 : 2);
                            plc = new Plc(S7Config.CpuType, S7Config.PlcIp, 0, slot);
                        }
                    }

                    // 检查PLC是否连接，如果没有连接，则尝试重新连接
                    if (!plc.IsConnected)
                    {
                        try
                        {
                            LogHelper.Debug($"{Config.Name} 连接中...");
                            plc.Open();
                            if (!plc.IsConnected)
                            {
                                LogHelper.Error($"{Config.Name} 无法连接到PLC：{S7Config.PlcIp}");
                                plc = null;
                            }
                            else
                            {
                                LogHelper.Debug($"{Config.Name} 连接成功");
                            }
                        }
                        catch (Exception e)
                        {
                            LogHelper.Error($"{Config.Name} 连接异常：{e.Message}");
                            lock (plcLock)
                            {
                                plc.Close();
                                plc = null;
                            }
                        }
                    }
                    // 等待一段时间再检查 PLC 连接，避免频繁重连
                    var waitCount = 300;
                    while (isStarting && waitCount > 0)
                    {
                        waitCount -= 1;
                        Thread.Sleep(10); // 等待时间可以根据实际需要调整
                    }
                }

                lock (plcLock)
                {
                    if (plc != null)
                    {
                        plc.Close(); // 确保线程结束时关闭 PLC 连接
                        plc = null;
                    }
                }

                isStarting = false;
                ModuleState = S7ModuleState.Stopped;
                LogHelper.Debug($"{Config.Name} PLC通道维护模块已停止");
            })
            { IsBackground = true };

            channelThread.Start();
        }

        // 启动单独线程来维护读取模块
        private void StartReadDynamicPropertyImpl()
        {
            if (isReading)
            {
                return;
            }

            List<S7PointItem> s7PointItems = GetS7Points();
            var intervalGroups = S7ReadIntervalGroup.GroupPointsByReadIntervalAndDbArea(s7PointItems, canRead: true);

            //int readInterval = S7Config.ReadInterval;

            LogHelper.Debug($"{Config.Name} PLC读取模板已启动");
            isReading = true;
            var hadRead = false;

            readingThread = new Thread(() =>
            {
                while (isReading)
                {
                    try
                    {
                        var dtNow = TimeHelper.GetNow();
                        // 进行 PLC 读取操作
                        hadRead = false;
                        lock (plcLock)
                        {
                            if (plc != null && plc.IsConnected)
                            {
                                foreach (var intervalGroup in intervalGroups)
                                {
                                    if (intervalGroup.ReadInterval > 0 && dtNow - intervalGroup.LastReadTime > intervalGroup.ReadInterval)
                                    {
                                        intervalGroup.LastReadTime = dtNow;
                                        foreach (var dbGroup in intervalGroup.DbGroups)
                                        {
                                            //LogHelper.Debug($"ReadBytes DataBlock: {DataType.DataBlock}, DBArea: {dbGroup.DBArea}, StartAdr: {dbGroup.StartAdr}, Count: {dbGroup.Count}");
                                            try
                                            {
                                                dbGroup.Data = null;
                                                var data = plc.ReadBytes(DataType.DataBlock, dbGroup.DBArea, dbGroup.StartAdr, dbGroup.Count);
                                                if (data != null && data.Length > 0)
                                                {
                                                    // 复制一份data到dbGroup
                                                    dbGroup.Data = new byte[data.Length];
                                                    data.CopyTo(dbGroup.Data, 0);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogHelper.Error($"PLC点位读取失败:{dbGroup.DBArea} - {dbGroup.StartAdr}，{ex.Message}");
                                            }
                                        }
                                    }
                                }
                                hadRead = true;
                            }
                        }

                        if (hadRead)
                        {
                            foreach (var intervalGroup in intervalGroups)
                            {
                                foreach (var dbGroup in intervalGroup.DbGroups)
                                {
                                    var data = dbGroup.Data;
                                    if (data != null)
                                    {
                                        var startAdr = dbGroup.StartAdr;
                                        foreach (var point in dbGroup.Points)
                                        {
                                            var length = point.GetLength();
                                            var index = point.ByteOffset - startAdr;
                                            // 检测数据长度是否足够
                                            if (index + length > data.Length)
                                            {
                                                // debug: 数据长度不足，跳过
                                                continue;
                                            }
                                            var variableType = point.VariableType;
                                            switch (variableType)
                                            {
                                                case VariableType.Bit:
                                                    {
                                                        if (point.BitOffset < 8)
                                                        {
                                                            bool value = (data[index] & (1 << point.BitOffset)) != 0;
                                                            int intValue = value ? 1 : 0;
                                                            S7PointItem.SetPointValue(point, intValue);
                                                        }
                                                    }
                                                    break;
                                                case VariableType.Byte:
                                                    {
                                                        int intValue = data[index];
                                                        S7PointItem.SetPointValue(point, intValue);
                                                    }
                                                    break;
                                                case VariableType.Int16:
                                                    {
                                                        int intValue = (short)((int)(data[index+1]) | ((int)(data[index]) << 8));
                                                        S7PointItem.SetPointValue(point, intValue);
                                                    }
                                                    break;
                                                case VariableType.Int32:
                                                    {
                                                        int intValue = data[index] << 24 | data[index+1] << 16 | data[index+2] << 8 | data[index+3];
                                                        S7PointItem.SetPointValue(point, intValue);
                                                    }
                                                    break;
                                                case VariableType.Float32:
                                                    {
                                                        if (BitConverter.IsLittleEndian)
                                                        {
                                                            // 转换为大端
                                                            var tmp1 = data[index];
                                                            data[index] = data[index + 3];
                                                            data[index + 3] = tmp1;
                                                            var tmp2 = data[index + 1];
                                                            data[index + 1] = data[index + 2];
                                                            data[index + 2] = tmp2;
                                                        }
                                                        double doubleValue = BitConverter.ToSingle(data, index);
                                                        S7PointItem.SetPointValue(point, doubleValue);
                                                    }
                                                    break;
                                                case VariableType.Float64:
                                                    {
                                                        if (BitConverter.IsLittleEndian)
                                                        {
                                                            // 转换为大端
                                                            Array.Reverse(data, index, 8);
                                                        }
                                                        double doubleValue = BitConverter.ToDouble(data, index);
                                                        S7PointItem.SetPointValue(point, doubleValue);
                                                    }
                                                    break;
                                                case VariableType.S7String:
                                                    {
                                                        if (length > 2)
                                                        {
                                                            int size = data[index];
                                                            int stringLength = data[index + 1];
                                                            if (size >= stringLength && length >= stringLength)
                                                            {
                                                                try
                                                                {
                                                                    // 字符集默认为ASCII，可以为GBK、UTF8
                                                                    string stringValue = Encoding.ASCII.GetString(data, index+2, stringLength);
                                                                    S7PointItem.SetPointValue(point, stringValue);
                                                                }
                                                                catch (Exception)
                                                                {
                                                                    // debug: 跳过解析失败的数据
                                                                    //throw new PlcException(ErrorCode.ReadData,
                                                                    //    $"Failed to parse {VarType.S7String} from data. Following fields were read: size: '{size}', actual length: '{length}', total number of bytes (including header): '{bytes.Length}'.",
                                                                    //    e);
                                                                }
                                                            }
                                                        }
                                                    }
                                                    break;
                                                case VariableType.DateTimeLong:
                                                    {
                                                        try
                                                        {
                                                            var year = AssertRangeInclusive(Word.FromBytes(data[index + 1], data[index]), 1970, 2262, "year");
                                                            var month = AssertRangeInclusive(data[index + 2], 1, 12, "month");
                                                            var day = AssertRangeInclusive(data[index + 3], 1, 31, "day of month");
                                                            var dayOfWeek = AssertRangeInclusive(data[index + 4], 1, 7, "day of week");
                                                            var hour = AssertRangeInclusive(data[index + 5], 0, 23, "hour");
                                                            var minute = AssertRangeInclusive(data[index + 6], 0, 59, "minute");
                                                            var second = AssertRangeInclusive(data[index + 7], 0, 59, "second");
                                                            var nanoseconds = AssertRangeInclusive<uint>(DWord.FromBytes(data[index + 11], data[index + 10], data[index + 9], data[index + 8]), 0,
                                                                999999999, "nanoseconds");
                                                            var time = new System.DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
                                                            time = time.AddTicks(nanoseconds / 100);
                                                            long longValue = TimeHelper.GetMs(time);
                                                            S7PointItem.SetPointValue(point, longValue);
                                                        }
                                                        catch (Exception)
                                                        {
                                                            // debug: 跳过解析失败的数据
                                                        }
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    dbGroup.Data = null;
                                }
                            }

                            Interlocked.Exchange(ref ReadTime, dtNow);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"{Config.Name} 读取PLC数据时发生异常：{ex.Message}");
                        plcError = true;
                    }

                    Thread.Sleep(10); // 按配置的读取间隔等待
                }

                isReading = false;
                LogHelper.Debug($"{Config.Name} PLC读取模板已停止");
            })
            { IsBackground = true };

            readingThread.Start();
        }

        private static T AssertRangeInclusive<T>(T input, T min, T max, string field) where T : IComparable<T>
        {
            if (input.CompareTo(min) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(input), input,
                    $"Value '{input}' is lower than the minimum '{min}' allowed for {field}.");
            }

            if (input.CompareTo(max) > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(input), input,
                    $"Value '{input}' is higher than the maximum '{max}' allowed for {field}.");
            }

            return input;
        }

        // 停止读取模块，停止PLC通道
        public override void StopReadDynamicProperty()
        {
            // 检查是否已启动
            if (ModuleState != S7ModuleState.Started)
            {
                return;
            }

            ModuleState = S7ModuleState.Stopping;

            // 停止读取，等待线程结束
            isReading = false;
            readingThread.Join();

            // 关闭通道模块
            isStarting = false;
        }

        public bool GetPlcIsConnected()
        {
            lock (plcLock)
            {
                if (plc != null && plc.IsConnected)
                {
                    return true;
                }
            }
            return false;
        }

        public void SetDryRun(bool value)
        {
        }

        public List<S7PointItem> GetS7Points()
        {
            List<S7PointItem> res = new List<S7PointItem>();
            foreach (var pt in PointSheet.Points)
            {
                S7PointItem io = pt as S7PointItem;
                if (io != null)
                    res.Add(io);
            }
            return res;
        }

        protected override bool PointFromContextImpl(Dictionary<string, string> context, PointItem pointItem)
        {
            var s7pointItem = pointItem as S7PointItem;
            if (s7pointItem != null)
            {
                s7pointItem.DBArea = int.Parse(context["DBArea"]);
                s7pointItem.VariableType = (VariableType)Enum.Parse(typeof(VariableType), context["VariableType"], true);
                s7pointItem.Address = context["Address"];
                s7pointItem.StringLength = int.Parse(context["StringLength"]);
                s7pointItem.ReadInterval = int.Parse(context["ReadInterval"]);
                return true;
            }
            return false;
        }

        protected override bool PointToContextImpl(Dictionary<string, string> context, PointItem pointItem)
        {
            var s7pointItem = pointItem as S7PointItem;
            if (s7pointItem != null)
            {
                context["DBArea"] = s7pointItem.DBArea.ToString();
                context["VariableType"] = s7pointItem.VariableType.ToString();
                context["Address"] = s7pointItem.Address;
                context["StringLength"] = s7pointItem.StringLength.ToString();
                context["ReadInterval"] = s7pointItem.ReadInterval.ToString();
                return true;
            }
            return false;
        }

        protected override void LoadPointsAfter()
        {
            // 如果 PointSheet List<S7PointItem> 的设备为空，则创建设备
            var points = GetS7Points();
            foreach (var point in points)
            {
                if (VariableObjectManager.GetObject(point.DeviceName) == null)
                {
                    S7PointItem.CreateAndRegisterDevice(point.DeviceName, point.VariableType);
                }
            }
        }

        public override PointItem CreatePointItem(string deviceName, string variableName)
        {
            return new S7PointItem(deviceName, variableName);
        }

        public S7PointItem CreateS7PointItem(string deviceName, string variableName, string dbArea, VarType varType, string Address)
        {
            return new S7PointItem(deviceName, variableName);
        }

        protected override SendPointValueResult DoSendPointValue<TValue>(PointItem pointItem, TValue value)
        {
            if (pointItem is S7PointItem pt)
            {
                try
                {
                    lock (plcLock)
                    {
                        if (plc != null && plc.IsConnected)
                        {
                            DoSendPointValueImpl(pt, value);
                            return SendPointValueResult.SentAndAcknowledged;
                        }
                        else
                        {
                            return SendPointValueResult.SendFailed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    plcError = true;
                    LogHelper.Error($"{Config.Name} 写入点{pt.DeviceName}.{pt.VariableName}时发生异常：{ex.Message}");
                    return SendPointValueResult.SendFailed;
                }
            }
            return SendPointValueResult.InternalError;
        }

        protected void DoSendPointValueImpl<TValue>(S7PointItem pt, TValue value)
        {
            switch (pt.VariableType)
            {
                case VariableType.Bit:
                    {
                        if (value is bool b)
                        {
                            plc.WriteBit(DataType.DataBlock, pt.DBArea, pt.ByteOffset, pt.BitOffset, b);
                        }
                        else if (value is int i)
                        {
                            plc.WriteBit(DataType.DataBlock, pt.DBArea, pt.ByteOffset, pt.BitOffset, i == 1);
                        }
                        else
                        {
                            throw new ArgumentException($"Value必须是 bool 或 int 类型.");
                        }
                    }
                    break;
                case VariableType.Byte:
                    {
                        byte v;
                        if (value is byte bb)
                        {
                            v = bb;
                        }
                        else if (value is int i)
                        {
                            v = (byte)i;
                        }
                        else if (value is sbyte sb)
                        {
                            v = (byte)sb;
                        }
                        else if (value is string str)
                        {
                            v = byte.Parse(str);
                        }
                        else
                        {
                            throw new ArgumentException($"Value转换为 Byte 类型时发生异常.");
                        }
                        plc.WriteBytes(DataType.DataBlock, pt.DBArea, pt.ByteOffset, global::S7.Net.Types.Byte.ToByteArray(v));
                    }
                    break;
                case VariableType.Int16:
                    {
                        short v;
                        if (value is short s)
                        {
                            v = s;
                        }
                        else if (value is ushort us)
                        {
                            v = (short)us;
                        }
                        else if (value is int u)
                        {
                            v = (short)u;
                        }
                        else if (value is string str)
                        {
                            v = short.Parse(str);
                        }
                        else
                        {
                            throw new ArgumentException($"Value转换为 Int16 类型时发生异常.");
                        }
                        plc.WriteBytes(DataType.DataBlock, pt.DBArea, pt.ByteOffset, Int.ToByteArray(v));
                    }
                    break;
                case VariableType.Int32:
                    {
                        int v;
                        if (value is int i)
                        {
                            v = i;
                        }
                        else if (value is UInt32 ui)
                        {
                            v = (int)ui;
                        }
                        else if (value is bool b)
                        {
                            v = b ? 1 : 0;
                        }
                        else if (value is byte bb)
                        {
                            v = bb;
                        }
                        else if (value is sbyte sb)
                        {
                            v = sb;
                        }
                        else if (value is short s)
                        {
                            v = s;
                        }
                        else if (value is ushort us)
                        {
                            v = (int)us;
                        }
                        else if (value is long l)
                        {
                            v = (int)l;
                        }
                        else if (value is ulong ul)
                        {
                            v = (int)ul;
                        }
                        else if (value is float f)
                        {
                            v = (int)f;
                        }
                        else if (value is double d)
                        {
                            v = (int)d;
                        }
                        else if (value is string str)
                        {
                            v = int.Parse(str);
                        }
                        else
                        {
                            throw new ArgumentException($"Value转换为 Int32 类型时发生异常.");
                        }
                        plc.WriteBytes(DataType.DataBlock, pt.DBArea, pt.ByteOffset, DInt.ToByteArray(v));
                    }
                    break;
                case VariableType.Float32:
                    {
                        float v;
                        if (value is float f)
                        {
                            v = f;
                        }
                        else if (value is double d)
                        {
                            v = (float)d;
                        }
                        else if (value is int i)
                        {
                            v = (float)i;
                        }
                        else if (value is string str)
                        {
                            v = float.Parse(str);
                        }
                        else
                        {
                            throw new ArgumentException($"Value转换为 Float32 类型时发生异常.");
                        }
                        plc.WriteBytes(DataType.DataBlock, pt.DBArea, pt.ByteOffset, Real.ToByteArray(v));
                    }
                    break;
                case VariableType.Float64:
                    {
                        double v;
                        if (value is double d)
                        {
                            v = d;
                        }
                        else if (value is float f)
                        {
                            v = (double)f;
                        }
                        else if (value is int i)
                        {
                            v = (double)i;
                        }
                        else if (value is string str)
                        {
                            v = double.Parse(str);
                        }
                        else
                        {
                            throw new ArgumentException($"Value转换为 Float64 类型时发生异常.");
                        }
                        plc.WriteBytes(DataType.DataBlock, pt.DBArea, pt.ByteOffset, LReal.ToByteArray(v));
                    }
                    break;
                case VariableType.S7String:
                    {
                        if (value is string s)
                        {
                            if ((object)s != null)
                            {
                                if (s.Length <= pt.GetLength())
                                {
                                    var buf = S7String.ToByteArray(s, pt.GetLength());
                                    plc.WriteBytes(DataType.DataBlock, pt.DBArea, pt.ByteOffset, buf);
                                }
                            }
                            else
                            {
                                throw new ArgumentException($"Value不能为null.");
                            }
                        }
                        else
                        {
                            throw new ArgumentException($"Value必须是 string 类型.");
                        }
                    }
                    break;
                case VariableType.DateTimeLong:
                    {
                        System.DateTime v;
                        if (value is string s)
                        {
                            if (!TimeHelper.TryParseDateTime(s, out v))
                            {
                                throw new ArgumentException($"Value[{s}]转换为DateTime失败.");
                            }
                        }
                        else if (value is long l)
                        {
                            v = TimeHelper.GetDateTime(l);
                        }
                        else if (value is ulong ul)
                        {
                            v = TimeHelper.GetDateTime((long)ul);
                        }
                        else if (value is System.DateTime dt)
                        {
                            v = dt;
                        }
                        else
                        {
                            throw new ArgumentException($"Value必须是 long 或 string 类型.");
                        }
                        plc.WriteBytes(DataType.DataBlock, pt.DBArea, pt.ByteOffset, DateTimeLong.ToByteArray(v));
                    }
                    break;
                default:
                    throw new ArgumentException($"未知的变量类型{pt.VariableType}.");
            }
        }

        protected override (SendPointValueResult, TValue) DoReadPointValue<TValue>(PointItem pointItem, TValue defalutValue)
        {
            return (SendPointValueResult.InternalError, defalutValue);
        }

        public static S7PointItem FindPoint(List<S7PointItem> s7PointItems, int db, int startByteAdr, byte bitAdr)
        {
            // 在s7PointItems中找：pt.DBArea、pt.ByteOffset、pt.BitOffset 与 db、startByteAdr、bitAdr 相同
            foreach (var pt in s7PointItems)
            {
                if (pt.DBArea == db && pt.ByteOffset == startByteAdr && pt.BitOffset == bitAdr)
                {
                    return pt;
                }
            }
            return null;
        }

        public S7PointItem FindPoint(int db, int startByteAdr, byte bitAdr)
        {
            foreach (var pt in PointSheet.Points)
            {
                S7PointItem io = pt as S7PointItem;
                if (io != null)
                {
                    if (io.DBArea == db && io.ByteOffset == startByteAdr && io.BitOffset == bitAdr)
                    {
                        return io;
                    }
                }
            }
            return null;
        }

        /// <returns.db>Address of the memory area (if you want to read DB1, this is set to 1). This must be set also for other memory area types: counters, timers,etc.</param>
        /// <returns.startByteAdr>Start byte address. If you want to read DB1.DBW200, this is 200.</param>
        /// <returns.count>Byte count, if you want to read 120 bytes, set this to 120.</param>
        public List<DBAreaInfo> GetS7AreaInfos()
        {
            // startByteAdr is From List<S7PointItem>, same DBArea, min GetByteOffset
            // count is From List<S7PointItem>, same DBArea, (max GetByteOffset) - (min GetByteOffset) + Length

            // 创建DBAreaInfo列表
            var areaInfos = new List<DBAreaInfo>();

            // 按DBArea分组S7PointItems
            var groupedPoints = GetS7Points().GroupBy(p => p.DBArea);

            foreach (var group in groupedPoints)
            {
                // 对于每个分组，找到最小和最大的ByteOffset
                var minByteOffset = group.Min(p => p.ByteOffset);
                var maxByteOffset = group.Max(p => p.ByteOffset + p.GetLength() - 1);

                // 计算需要读取的字节数
                var count = maxByteOffset - minByteOffset + 1;

                // 创建并添加DBAreaInfo到列表
                areaInfos.Add(new DBAreaInfo
                {
                    DB = group.Key,
                    StartByteAdr = minByteOffset,
                    Count = count
                });
            }

            return areaInfos;
        }

        [Obfuscation(Exclude = true)]
        // 验证s7PointItems安全合法
        public void CheckS7Points(List<S7PointItem> s7PointItems)
        {
            // pt.DBArea、pt.ByteOffset、pt.BitOffset 不能重复
            // Check for duplicate DBArea, ByteOffset, and BitOffset in the list of S7PointItems

            var duplicates = s7PointItems
            .GroupBy(item => new { item.DBArea, item.ByteOffset, item.BitOffset })  // 按三个字段分组
            .Where(group => group.Count() > 1)  // 筛选重复组（出现次数 >1）
            .SelectMany(group => group.Skip(1)) // 跳过每组第一个元素，保留重复项
            .ToList();

            if (duplicates.Count > 0)
            {
                // Handle duplicates found、ArgumentOutOfRangeException、ArgumentNullException
                
                for (int i = 0; i < duplicates.Count; i++)
                {
                    EventLogHelper.Error(LOG_TYPE.PLC, "S7协议", $" 点表中存在重复的项 Name :{duplicates[i].DeviceName} ");
                }
                throw new ArgumentException($"{Config.Name} 点表中存在重复的DBArea、ByteOffset、BitOffset:{string.Join(",", duplicates)}");
            }
            // pt.DBArea >= 0 && <= 255 
            // pt.ByteOffset >= 0 && <= 65535
            // pt.BitOffset >= 0 && <= 7
            // pt.StringLength >= 0 && <= 254
            foreach (var pt in s7PointItems)
            {
                var result = S7PointItem.CheckS7Point(pt);
                if (!string.IsNullOrEmpty(result))
                {
                    throw new ArgumentException($"{Config.Name} 点{pt.DeviceName}检查失败：{result}");
                }
            }
        }



        // 注册工厂
        public static void RegisterFactory()
        {
            DriverModuleFactoryCenter.RegisterFactory(StringHelper.ExtractShortNameAfterDot(typeof(S7Driver).Name), "板卡驱动模块",
            (driverModuleConfig) => new S7Driver(driverModuleConfig));
        }

    }

}
