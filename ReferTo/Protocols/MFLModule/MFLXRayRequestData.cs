using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonInterfaces;

namespace CxVisual.Platform.Protocols.Mfl
{
    /// <summary>
    /// X-Ray 发起判定请求数据详情（功能码：0x00000101）
    ///
    /// 改造目标：参考 MFLClientIDResponseData，线上只收发通信字段：
    ///   Version(u32)
    ///   KVByteLen(u32) + KVBytes(UTF8)     // KV 每行 k=v；value 建议 Base64（由 KvText 负责）
    ///   BinCount(u32)
    ///   BinByteLen(u32) + BinBytes
    ///
    /// BinBytes 内部：
    ///   重复 BinCount 次：
    ///     Type(u32) + Len(u32) + Data(bytes)
    /// </summary>
    public class MFLXRayRequestData
    {
        // ===== 版本 =====
        // 业务版本，当前业务字段不兼容时就报错，并不作解析与处理
        public const uint CurrentVersion = 0x00020004;

        // ===== KV Keys（尽量短，工业传输省字节）=====
        public const string K_LineNumber = "LN";
        public const string K_BatchNumber = "BN";
        public const string K_Position = "POS";
        public const string K_TrayPos = "TP";

        public const string K_MarkTitlesContext = "MTC";    // SN|线号……检测时间|AIResult
        public const string K_IRes1 = "IR1";
        public const string K_ImageCount = "IC";
        public const string K_RemoteDisplayAI = "RDA";

        public const string K_AIResultTexts = "AIR";
        public const string K_AttachedAIResultTexts1 = "AT1";
        public const string K_AttachedAIResultTexts2 = "AT2";

        // ===== Bin Block Types =====
        public const uint B_ImageData = 1;
        public const uint B_AIResultBoxes = 2;

        // ===== 通信字段（唯一收发）=====
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
        /// KVs：KVString 的解析载体；所有旧字段/辅助字段都走这里。
        /// </summary>
        public KvText KVs { get; private set; } = new KvText();

        /// <summary>
        /// 可选：把 BinData 解析成块列表（建议保留 raw BinData，同时提供结构化读取）
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
        //  业务字段（全部通过 KVs / BinBlocks 映射）
        // ============================================================

        public uint LineNumber
        {
            get { return (uint)Math.Max(0, KVs.GetInt(K_LineNumber, 0)); }
            set { KVs.Set(K_LineNumber, SafeToInt(value)); }
        }

        public uint BatchNumber
        {
            get { return (uint)Math.Max(0, KVs.GetInt(K_BatchNumber, 0)); }
            set { KVs.Set(K_BatchNumber, SafeToInt(value)); }
        }

        public uint Position
        {
            get { return (uint)Math.Max(0, KVs.GetInt(K_Position, 0)); }
            set { KVs.Set(K_Position, SafeToInt(value)); }
        }

        public uint TrayPos
        {
            get { return (uint)Math.Max(0, KVs.GetInt(K_TrayPos, 0)); }
            set { KVs.Set(K_TrayPos, SafeToInt(value)); }
        }

        public string MarkTitlesContext
        {
            get { return KVs.GetString(K_MarkTitlesContext, string.Empty); }
            set { KVs.Set(K_MarkTitlesContext, value ?? string.Empty); }
        }

        // 图像数据大小（你原来的字段名保留语义）
        public uint IRes1
        {
            get { return (uint)Math.Max(0, KVs.GetInt(K_IRes1, 0)); }
            set { KVs.Set(K_IRes1, SafeToInt(value)); }
        }

        public uint ImageCount
        {
            get { return (uint)Math.Max(0, KVs.GetInt(K_ImageCount, 0)); }
            set { KVs.Set(K_ImageCount, SafeToInt(value)); }
        }

        public uint RemoteDisplayAI
        {
            get { return (uint)Math.Max(0, KVs.GetInt(K_RemoteDisplayAI, 0)); }
            set { KVs.Set(K_RemoteDisplayAI, SafeToInt(value)); }
        }

        public string AIResultTexts
        {
            get { return KVs.GetString(K_AIResultTexts, string.Empty); }
            set { KVs.Set(K_AIResultTexts, value ?? string.Empty); }
        }

        public string AttachedAIResultTexts1
        {
            get { return KVs.GetString(K_AttachedAIResultTexts1, string.Empty); }
            set { KVs.Set(K_AttachedAIResultTexts1, value ?? string.Empty); }
        }

        public string AttachedAIResultTexts2
        {
            get { return KVs.GetString(K_AttachedAIResultTexts2, string.Empty); }
            set { KVs.Set(K_AttachedAIResultTexts2, value ?? string.Empty); }
        }

        // ImageData 走 BinBlocks
        public byte[] ImageData
        {
            get { return GetBinBlockData(B_ImageData); }
            set { SetBinBlockData(B_ImageData, value); }
        }

        // AIResultBoxes 走 BinBlocks
        public byte[] AIResultBoxes
        {
            get { return GetBinBlockData(B_AIResultBoxes); }
            set { SetBinBlockData(B_AIResultBoxes, value); }
        }

        // ============================================================
        //  双向转换（通信字段 <-> KVs/BinBlocks）
        // ============================================================

        public void LoadKVsFromKVString()
        {
            if (KVs == null) KVs = new KvText();
            // 与 MFLClientIDResponseData 一致：valueIsBase64:true
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
                if (!TryReadUInt32(BinData, ref idx, len, out uint dataLenU)) return false;

                if (dataLenU > int.MaxValue) return false;
                int dataLen = (int)dataLenU;

                if (idx + dataLen > len) return false;

                byte[] payload = new byte[dataLen];
                if (dataLen > 0)
                {
                    Buffer.BlockCopy(BinData, idx, payload, 0, dataLen);
                    idx += dataLen;
                }

                BinBlocks.Add(new BinBlock(type, payload));
            }

            // 工业：严格不允许尾巴垃圾
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

                WriteUInt32(buf, ref idx, b.Type);
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
        //  核心：TryParse / ToByteArray（只收发通信字段）
        // ============================================================

        public static bool TryParse(byte[] data, uint dataLength, out MFLXRayRequestData request)
        {
            request = null;

            if (data == null) return false;
            if (dataLength == 0) return false;
            if (dataLength > (uint)data.Length) return false;

            try
            {
                // 先读 version 来分流：LegacyVersion 走旧解析；否则走新解析
                if (dataLength < 4) return false;
                uint version = BitConverter.ToUInt32(data, 0);

                if (version != CurrentVersion)
                {
                    LogHelper.Error($"MFLXRayRequestData 版本不匹配，请求版本：{CurrentVersion}，应该版本：{version}");
                    return false;
                }

                // 新版（通信字段）
                {
                    int idx = 0;
                    int limit = (int)dataLength;

                    if (!TryReadUInt32(data, ref idx, limit, out uint ver)) return false;

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

                    // 工业：严格——不允许多余尾巴
                    if (idx != limit) return false;

                    var obj = new MFLXRayRequestData
                    {
                        Version = ver,
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

                    request = obj;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public byte[] ToByteArray()
        {
            // 工业：确保 KVString 与 KVs 同步
            BuildKVStringFromKVs();

            // 工业：BinBlocks 有值就自动打包
            if (BinBlocks != null && BinBlocks.Count > 0)
            {
                BuildBinDataFromBinBlocks();
            }

            byte[] kvBytes = string.IsNullOrEmpty(KVString) ? new byte[0] : Encoding.UTF8.GetBytes(KVString);
            byte[] binBytes = BinData ?? new byte[0];

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

        // ============================================================
        //  BinBlocks 便捷访问
        // ============================================================

        private byte[] GetBinBlockData(uint type)
        {
            if (BinBlocks == null || BinBlocks.Count == 0) return new byte[0];

            for (int i = 0; i < BinBlocks.Count; i++)
            {
                if (BinBlocks[i].Type == type)
                {
                    return BinBlocks[i].Data ?? new byte[0];
                }
            }
            return new byte[0];
        }

        private void SetBinBlockData(uint type, byte[] data)
        {
            if (BinBlocks == null) BinBlocks = new List<BinBlock>();

            for (int i = 0; i < BinBlocks.Count; i++)
            {
                if (BinBlocks[i].Type == type)
                {
                    BinBlocks[i] = new BinBlock(type, data);
                    return;
                }
            }

            BinBlocks.Add(new BinBlock(type, data));
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

        private static int SafeToInt(uint v)
        {
            return (v > int.MaxValue) ? int.MaxValue : (int)v;
        }

        public override string ToString()
        {
            var imgLen = (ImageData == null) ? 0 : ImageData.Length;
            return $"Line: {LineNumber}, Batch: {BatchNumber}, Position: {Position}, TrayPos: {TrayPos}, " +
                   $"MarkTitlesContext: {MarkTitlesContext}, ImageSize: {imgLen} bytes";
        }

    }

}
