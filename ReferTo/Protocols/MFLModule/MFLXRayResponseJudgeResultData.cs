using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonInterfaces;

namespace CxVisual.Platform.Protocols.Mfl
{
    /// <summary>
    /// client 回复判定结果数据详情（功能码：0x00000102）
    ///
    /// 改造目标：参考 MFLClientIDResponseData，只以 Version/KVString/BinCount/BinData 为“通信字段”收发；
    /// 其余业务字段全部走 KVs（KV文本）进行解析/写回。
    ///
    /// 协议字节格式（LE）：
    ///   Version(u32)
    ///   KVByteLen(u32) + KVBytes(UTF8)
    ///   BinCount(u32)
    ///   BinByteLen(u32) + BinBytes
    ///
    /// BinBytes 内部：
    ///   重复 BinCount 次：
    ///     Type(u32) + Len(u32) + Data(bytes)
    /// </summary>
    public class MFLXRayResponseJudgeResultData
    {
        public const uint CurrentVersion = 0x00020004;

        // ========= KV Keys（尽量短，现场传输省流量；含义固定后不要改）=========
        public const string K_Line = "L";
        public const string K_Batch = "B";
        public const string K_Position = "P";
        public const string K_Result = "R";
        public const string K_UserName = "U";
        public const string K_TrayPos = "TP";
        public const string K_JudgeTime = "JT";
        public const string K_JudgeTime_Image = "JTI";
        public const string K_JudgeTime_Select = "JTS";
        public const string K_IsJudgeTimeout = "TO";

        public const string K_DrawLineArray = "DLA";
        public const string K_DrawAreaArray = "DAA";

        // （可选兼容）若你希望 DrawState 也能走 KV（Base64），可启用该 key
        // 建议默认走 BinBlock，更规范
        public const string K_DrawStateBase64 = "DSB64";

        // ========= BinBlock Types =========
        public const uint BinType_DrawState = 0x00000001;

        // ========= 通信字段（唯一收发）=========
        public uint Version { get; set; } = CurrentVersion;

        /// <summary>
        /// KVString：逐行 k=v 文本（UTF8）。TryParse 后会 Load 到 KVs。
        /// </summary>
        public string KVString { get; set; } = string.Empty;

        /// <summary>
        /// 二进制块数量（与 BinData 内块数量对应）
        /// </summary>
        public uint BinCount { get; set; }

        /// <summary>
        /// BinData：二进制打包区（Type+Len+Data 重复 BinCount 次）
        /// </summary>
        public byte[] BinData { get; set; } = new byte[0];

        /// <summary>
        /// KVs：KVString 的解析载体；所有业务字段都走这里。
        /// </summary>
        public KvText KVs { get; private set; } = new KvText();

        /// <summary>
        /// 可选：把 BinData 解析成块列表使用（保留 raw BinData，同时提供结构化读取）
        /// </summary>
        public List<BinBlock> BinBlocks { get; private set; } = new List<BinBlock>();

        public sealed class BinBlock
        {
            public uint Type { get; private set; }
            public byte[] Data { get; private set; }

            public BinBlock(uint type, byte[] data)
            {
                Type = type;
                Data = data ?? new byte[0];
            }
        }

        // ============================================================
        //  双向转换（业务字段 <-> 通信字段）
        // ============================================================

        public void LoadKVsFromKVString()
        {
            if (KVs == null) KVs = new KvText();
            KVs.LoadFromText(KVString, valueIsBase64: true);
        }

        public void BuildKVStringFromKVs()
        {
            if (KVs == null) KVs = new KvText();
            KVString = KVs.ToText(valueIsBase64: true);
        }

        public bool TryParseBinBlocksFromBinData()
        {
            BinBlocks.Clear();

            if (BinCount == 0)
            {
                return true;
            }

            if (BinData == null) return false;

            int idx = 0;
            int len = BinData.Length;

            for (uint i = 0; i < BinCount; i++)
            {
                if (!TryReadUInt32(BinData, ref idx, len, out uint type)) return false;
                if (!TryReadUInt32(BinData, ref idx, len, out uint dataLen)) return false;

                if (dataLen > int.MaxValue) return false;
                int dlen = (int)dataLen;

                if (idx < 0 || idx + dlen > len) return false;

                byte[] payload = new byte[dlen];
                if (dlen > 0)
                {
                    Buffer.BlockCopy(BinData, idx, payload, 0, dlen);
                    idx += dlen;
                }

                BinBlocks.Add(new BinBlock(type, payload));
            }

            // 严格：不允许尾巴垃圾字节
            if (idx != len) return false;

            return true;
        }

        public void BuildBinDataFromBinBlocks()
        {
            if (BinBlocks == null || BinBlocks.Count == 0)
            {
                BinCount = 0;
                BinData = new byte[0];
                return;
            }

            long total = 0;
            for (int i = 0; i < BinBlocks.Count; i++)
            {
                var b = BinBlocks[i];
                int payloadLen = (b.Data == null) ? 0 : b.Data.Length;
                total += 8L + payloadLen;
                if (total > int.MaxValue) throw new InvalidOperationException("BinData too large.");
            }

            byte[] buf = new byte[(int)total];
            int idx = 0;

            for (int i = 0; i < BinBlocks.Count; i++)
            {
                var b = BinBlocks[i];
                byte[] payload = b.Data ?? new byte[0];

                WriteUInt32(buf, ref idx, (uint)b.Type);
                WriteUInt32(buf, ref idx, (uint)payload.Length);

                if (payload.Length > 0)
                {
                    Buffer.BlockCopy(payload, 0, buf, idx, payload.Length);
                    idx += payload.Length;
                }
            }

            BinCount = (uint)BinBlocks.Count;
            BinData = buf;
        }

        // ============================================================
        //  业务字段：全部属性化（get 从KVs取，set写入KVs）
        // ============================================================

        public uint Line
        {
            get { return (uint)KVs.GetInt(K_Line, 0); }
            set { KVs.Set(K_Line, (int)value); }
        }

        public uint Batch
        {
            get { return (uint)KVs.GetInt(K_Batch, 0); }
            set { KVs.Set(K_Batch, (int)value); }
        }

        public uint Position
        {
            get { return (uint)KVs.GetInt(K_Position, 0); }
            set { KVs.Set(K_Position, (int)value); }
        }

        public string Result
        {
            get { return KVs.GetString(K_Result, string.Empty); }
            set { KVs.Set(K_Result, value ?? string.Empty); }
        }

        public string UserName
        {
            get { return KVs.GetString(K_UserName, string.Empty); }
            set { KVs.Set(K_UserName, value ?? string.Empty); }
        }

        public uint TrayPos
        {
            get { return (uint)KVs.GetInt(K_TrayPos, 0); }
            set { KVs.Set(K_TrayPos, (int)value); }
        }

        /// <summary>判定时长</summary>
        public double JudgeTime
        {
            get { return KVs.GetDouble(K_JudgeTime, 0.0); }
            set { KVs.Set(K_JudgeTime, value); }
        }

        /// <summary>判定时长---看图时长</summary>
        public double JudgeTime_image
        {
            get { return KVs.GetDouble(K_JudgeTime_Image, 0.0); }
            set { KVs.Set(K_JudgeTime_Image, value); }
        }

        /// <summary>判定时长---选择结果时长</summary>
        public double JudgeTime_select
        {
            get { return KVs.GetDouble(K_JudgeTime_Select, 0.0); }
            set { KVs.Set(K_JudgeTime_Select, value); }
        }

        /// <summary>判定是否是OP操作超时，1为是，0为否</summary>
        public uint isJudgeTimeout
        {
            get { return (uint)KVs.GetInt(K_IsJudgeTimeout, 0); }
            set { KVs.Set(K_IsJudgeTimeout, (int)value); }
        }

        private double[] _cachedDrewLine;
        private string _cachedDrewLineString;
        /// <summary>
        /// 线段数组：所有线段的长度集合
        /// 传输格式：逗号分隔的double数组，Base64编码
        /// 示例：线段的长度值 [125.5, 130.2, 118.7]
        /// 注意：数据的进一步处理（计算最小值、最大值）应在接收端的业务逻辑中完成
        /// </summary>
        public double[] DrewLine
        {
            get
            {
                string lineStr = KVs.GetString(K_DrawLineArray, string.Empty);
                if (lineStr != _cachedDrewLineString)
                {
                    _cachedDrewLine = ParseDoubleArray(lineStr);
                    _cachedDrewLineString = lineStr;
                }
                return _cachedDrewLine;
            }
            set
            {
                string lineStr = value != null ? string.Join(",", value.Select(v => v.ToString("F3"))) : string.Empty;
                KVs.Set(K_DrawLineArray, lineStr);
                _cachedDrewLine = value;
                _cachedDrewLineString = lineStr;
            }
        }

        /// <summary>
        /// 面积数组：所有面积的集合
        /// 传输格式：逗号分隔的double数组，Base64编码
        /// 示例：面积值 [2450.75, 2550.20, 2400.15]
        /// </summary>
        public double[] DrewArea
        {
            get
            {
                string areaStr = KVs.GetString(K_DrawAreaArray, string.Empty);
                return ParseDoubleArray(areaStr);
            }
            set
            {
                string areaStr = value != null ? string.Join(",", value.Select(v => v.ToString("F3"))) : string.Empty;
                KVs.Set(K_DrawAreaArray, areaStr);
            }
        }

        /// <summary>
        /// 解析逗号分隔的double数组字符串
        /// </summary>
        private double[] ParseDoubleArray(string str)
        {
            if (string.IsNullOrEmpty(str))
                return new double[0];

            try
            {
                return str.Split(',')
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => double.TryParse(s.Trim(), out double val) ? val : 0.0)
                    .ToArray();
            }
            catch
            {
                return new double[0];
            }
        }

        /// <summary>
        /// 画框、画圆、尺寸等二进制序列化内容
        /// 默认走 BinBlock(Type=BinType_DrawState)。
        /// </summary>
        public byte[] DrawState { get; set; }

        // ============================================================
        //  DrawState：BinBlock <-> 属性
        // ============================================================

        private void LoadDrawStateFromBinBlocksOrKV()
        {
            // 1) 优先从 BinBlocks 取
            if (BinBlocks != null && BinBlocks.Count > 0)
            {
                for (int i = 0; i < BinBlocks.Count; i++)
                {
                    if (BinBlocks[i].Type == BinType_DrawState)
                    {
                        DrawState = BinBlocks[i].Data ?? new byte[0];
                        return;
                    }
                }
            }

            // 2) 可选兼容：若旧现场把 DrawState 放 KV (Base64)（不推荐，但可救急）
            string dsB64 = KVs.GetString(K_DrawStateBase64, string.Empty);
            if (!string.IsNullOrEmpty(dsB64))
            {
                try
                {
                    DrawState = Convert.FromBase64String(dsB64);
                }
                catch
                {
                    DrawState = null;
                }
            }
        }

        private void BuildBinBlocksFromDrawState()
        {
            // 保守：不清空别的块，只保证 DrawState 块存在且最新
            if (BinBlocks == null) BinBlocks = new List<BinBlock>();

            // 移除旧的 DrawState 块
            for (int i = BinBlocks.Count - 1; i >= 0; i--)
            {
                if (BinBlocks[i].Type == BinType_DrawState)
                    BinBlocks.RemoveAt(i);
            }

            if (DrawState != null && DrawState.Length > 0)
            {
                BinBlocks.Add(new BinBlock(BinType_DrawState, DrawState));
            }
        }

        // ============================================================
        //  核心：TryParse / ToByteArray（只收发通信字段）
        // ============================================================

        public static bool TryParse(byte[] data, uint dataLength, out MFLXRayResponseJudgeResultData response)
        {
            response = null;

            if (data == null) return false;
            if (dataLength == 0) return false;
            if (dataLength > (uint)data.Length) return false;

            try
            {
                int idx = 0;
                int limit = (int)dataLength;

                if (!TryReadUInt32(data, ref idx, limit, out uint version)) return false;

                if (version != CurrentVersion)
                {
                    LogHelper.Error($"MFLXRayResponseJudgeResultData 版本不匹配，请求版本：{CurrentVersion}，应该版本：{version}");
                    return false;
                }

                if (!TryReadUInt32(data, ref idx, limit, out uint kvLenU)) return false;
                if (kvLenU > int.MaxValue) return false;
                int kvLen = (int)kvLenU;
                if (idx + kvLen > limit) return false;

                string kvString = kvLen == 0 ? string.Empty : Encoding.UTF8.GetString(data, idx, kvLen);
                idx += kvLen;

                if (!TryReadUInt32(data, ref idx, limit, out uint binCount)) return false;

                if (!TryReadUInt32(data, ref idx, limit, out uint binLenU)) return false;
                if (binLenU > int.MaxValue) return false;
                int binLen = (int)binLenU;
                if (idx + binLen > limit) return false;

                byte[] binData = new byte[binLen];
                if (binLen > 0)
                {
                    Buffer.BlockCopy(data, idx, binData, 0, binLen);
                    idx += binLen;
                }

                // 严格：不允许多余尾巴
                if (idx != limit) return false;

                var obj = new MFLXRayResponseJudgeResultData
                {
                    Version = version,
                    KVString = kvString ?? string.Empty,
                    BinCount = binCount,
                    BinData = binData ?? new byte[0],
                };

                obj.LoadKVsFromKVString();

                if (obj.BinCount > 0)
                {
                    if (!obj.TryParseBinBlocksFromBinData())
                        return false;
                }

                obj.LoadDrawStateFromBinBlocksOrKV();

                response = obj;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[] ToByteArray()
        {
            // 确保 KVString 与 KVs 一致
            BuildKVStringFromKVs();

            // 将 DrawState 写入 BinBlocks
            BuildBinBlocksFromDrawState();

            // 如果业务只填 BinBlocks，就自动打包
            if (BinBlocks != null && BinBlocks.Count > 0)
            {
                BuildBinDataFromBinBlocks();
            }
            else
            {
                // 没有块时，保持空
                BinCount = 0;
                BinData = new byte[0];
            }

            byte[] kvBytes = string.IsNullOrEmpty(KVString) ? new byte[0] : Encoding.UTF8.GetBytes(KVString);
            byte[] binBytes = BinData ?? new byte[0];

            // 同步 BinCount（以 BinBlocks 为准）
            if (BinBlocks != null && BinBlocks.Count > 0)
            {
                BinCount = (uint)BinBlocks.Count;
            }

            int totalLen = 4 + 4 + kvBytes.Length + 4 + 4 + binBytes.Length;
            byte[] buf = new byte[totalLen];

            int idx = 0;
            WriteUInt32(buf, ref idx, Version);

            WriteUInt32(buf, ref idx, (uint)kvBytes.Length);
            if (kvBytes.Length > 0)
            {
                Buffer.BlockCopy(kvBytes, 0, buf, idx, kvBytes.Length);
                idx += kvBytes.Length;
            }

            WriteUInt32(buf, ref idx, BinCount);

            WriteUInt32(buf, ref idx, (uint)binBytes.Length);
            if (binBytes.Length > 0)
            {
                Buffer.BlockCopy(binBytes, 0, buf, idx, binBytes.Length);
                idx += binBytes.Length;
            }

            return buf;
        }

        public override string ToString()
        {
            return $"Line: {Line}, Batch: {Batch}, Position: {Position}, Result: {Result}, UserName: {UserName}, TrayPos: {TrayPos}, JT: {JudgeTime}, TO: {isJudgeTimeout}";
        }

        // ============================================================
        //  底层安全读写（LE）
        // ============================================================

        private static bool TryReadUInt32(byte[] buf, ref int idx, int limit, out uint value)
        {
            value = 0;
            if (buf == null) return false;
            if (idx < 0) return false;
            if (idx + 4 > limit) return false;

            value = (uint)(buf[idx]
                | (buf[idx + 1] << 8)
                | (buf[idx + 2] << 16)
                | (buf[idx + 3] << 24));
            idx += 4;
            return true;
        }

        private static void WriteUInt32(byte[] buf, ref int idx, uint value)
        {
            buf[idx] = (byte)(value & 0xFF);
            buf[idx + 1] = (byte)((value >> 8) & 0xFF);
            buf[idx + 2] = (byte)((value >> 16) & 0xFF);
            buf[idx + 3] = (byte)((value >> 24) & 0xFF);
            idx += 4;
        }

    }

}
