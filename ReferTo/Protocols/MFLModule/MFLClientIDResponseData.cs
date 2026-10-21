using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonInterfaces;

namespace CxVisual.Platform.Protocols.Mfl
{
    /// <summary>
    /// 通信对象：只以 Version/KVString/BinCount/BinData 为“通信字段”收发。
    /// 其余“旧字段/辅助字段”一律通过 KVs 属性(get/set) 解析/写回。
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
    public class MFLClientIDResponseData
    {
        public const uint CurrentVersion = 0x00020004;

        // ====== 你点名的 KV keys（保持不变）======
        public const string K_DefectList = "DL";                // 缺陷列表
        public const string K_ImageUnitPixelSize = "UPS";       // 图像像素的实际尺寸大小
        public const string K_FontMeasureSize = "FMS";          // 画图：字符
        public const string K_DrawLineWidth = "DLW";            // 画图：行宽
        public const string K_TimedReplyOK = "TROK";            // 定时回复：OK
        public const string K_TimedAfterChecked = "TAC";        // 定时
        public const string K_ProductTableText = "PTT";     // 产品表 Base64（CSV → Base64），大文本，走 KV，不走 Bin
        public const string K_CurrentProductId = "CPI";         // 当前产品 Id

        // （可选）示例：多线放大比（按你原来的命名习惯留一组）
        public const string K_LineCount = "LC";
        public const string K_FodFddRatioPrefix = "FFR";        // e.g. FFR0 / FFR1 / ...

        // ====== 通信字段（唯一收发）======
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
        /// 可选：把 BinData 解析成块列表使用（工业上建议保留 raw BinData，同时提供结构化读取）
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
        //  双向转换（辅助字段 <-> 通信字段）
        // ============================================================

        /// <summary>
        /// 通信字段 -> KVs（TryParse 会调用）
        /// </summary>
        public void LoadKVsFromKVString()
        {
            if (KVs == null) KVs = new KvText();
            KVs.LoadFromText(KVString, valueIsBase64: true);
        }

        /// <summary>
        /// KVs/辅助字段 -> 通信字段（ToByteArray 会调用）
        /// </summary>
        public void BuildKVStringFromKVs()
        {
            if (KVs == null) KVs = new KvText();
            KVString = KVs.ToText(valueIsBase64: true);
        }

        /// <summary>
        /// 通信字段 -> BinBlocks（可选：TryParse 里严格校验时会用）
        /// </summary>
        public bool TryParseBinBlocksFromBinData()
        {
            BinBlocks.Clear();

            if (BinCount == 0)
            {
                // 允许 BinData 为空或不为空；但不解析
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
                Buffer.BlockCopy(BinData, idx, payload, 0, dlen);
                idx += dlen;

                BinBlocks.Add(new BinBlock(type, payload));
            }

            // 工业：建议严格——不允许有尾巴垃圾字节
            if (idx != len) return false;

            return true;
        }

        /// <summary>
        /// BinBlocks -> BinData（可选：ToByteArray 使用）
        /// </summary>
        public void BuildBinDataFromBinBlocks()
        {
            if (BinBlocks == null || BinBlocks.Count == 0)
            {
                BinCount = 0;
                BinData = new byte[0];
                return;
            }

            // 计算总长度：每块 8 + payload
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
        //  旧字段/辅助字段：全部属性化（get 从KVs取，set写入KVs）
        // ============================================================

        // 缺陷列表
        public string DefectListString
        {
            get { return KVs.GetString(K_DefectList, string.Empty); }
            set { KVs.Set(K_DefectList, value ?? string.Empty); }
        }

        // 图像像素的实际尺寸大小
        public double ImageUnitPixelActualSize
        {
            get { return KVs.GetDouble(K_ImageUnitPixelSize, 0.0); }
            set { KVs.Set(K_ImageUnitPixelSize, value); }
        }

        // 画图：字符
        public int FontMeasureSize
        {
            get { return KVs.GetInt(K_FontMeasureSize, 0); }
            set { KVs.Set(K_FontMeasureSize, value); }
        }

        // 画图：行宽
        public int DrawLineWidth
        {
            get { return KVs.GetInt(K_DrawLineWidth, 0); }
            set { KVs.Set(K_DrawLineWidth, value); }
        }

        // 定时回复：OK
        public int TimedReplyOK
        {
            get { return KVs.GetInt(K_TimedReplyOK, 0); }
            set { KVs.Set(K_TimedReplyOK, value); }
        }

        // 定时
        public int TimedAfterChecked
        {
            get { return KVs.GetInt(K_TimedAfterChecked, 0); }
            set { KVs.Set(K_TimedAfterChecked, value); }
        }

        // 产品表 Base64 文本（CSV → Base64）
        public string ProductTableText 
        {
            get { return KVs.GetString(K_ProductTableText, string.Empty); }
            set { KVs.Set(K_ProductTableText, value ?? string.Empty); }
        }

        // 示例：多线 ratios（可选能力：保留你原先的扩展意图）
        public double[] GetFodFddRatios()
        {
            int lc = KVs.GetInt(K_LineCount, 0);
            if (lc <= 0) return Array.Empty<double>();

            var result = new double[lc];
            for (int i = 0; i < lc; i++)
            {
                string key = K_FodFddRatioPrefix + i.ToString();
                result[i] = (KVs.GetDouble(key, 0.0));
            }
            return result;
        }

        public void SetFodFddRatios(double[] ratios)
        {
            if (ratios == null || ratios.Length == 0)
            {
                KVs.Set(K_LineCount, 0);
                // 不强制清旧 key，避免现场已有 key 被误删；如需可在此 Remove。
                return;
            }

            KVs.Set(K_LineCount, ratios.Length);
            for (int i = 0; i < ratios.Length; i++)
            {
                string key = K_FodFddRatioPrefix + i.ToString();
                KVs.Set(key, ratios[i]);
            }
        }

        // 产品表 Base64 文本（CSV → Base64）
        public string CurrentProductId
        {
            get { return KVs.GetString(K_CurrentProductId, string.Empty); }
            set { KVs.Set(K_CurrentProductId, value ?? string.Empty); }
        }

        // ============================================================
        //  核心：重写 TryParse / ToByteArray（只收发通信字段）
        // ============================================================

        public static bool TryParse(byte[] data, uint dataLength, out MFLClientIDResponseData response)
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

                if (!TryReadUInt32(data, ref idx, limit, out uint kvLenU)) return false;
                if (kvLenU > int.MaxValue) return false;
                int kvLen = (int)kvLenU;
                if (idx + kvLen > limit) return false;

                string kvString = kvLen == 0
                    ? string.Empty
                    : Encoding.UTF8.GetString(data, idx, kvLen);
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

                var obj = new MFLClientIDResponseData
                {
                    Version = version,
                    KVString = kvString ?? string.Empty,
                    BinCount = binCount,
                    BinData = binData ?? new byte[0],
                };

                obj.LoadKVsFromKVString();

                // 若 BinCount>0，要求 BinData 可成功按块解析
                if (obj.BinCount > 0)
                {
                    if (!obj.TryParseBinBlocksFromBinData())
                        return false;
                }

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
            // 工业：确保 KVString 与 KVs 一致
            BuildKVStringFromKVs();

            // 工业：如果业务只填 BinBlocks，就自动打包
            if (BinBlocks != null && BinBlocks.Count > 0)
            {
                BuildBinDataFromBinBlocks();
            }

            byte[] kvBytes = string.IsNullOrEmpty(KVString) ? new byte[0] : Encoding.UTF8.GetBytes(KVString);
            byte[] binBytes = BinData ?? new byte[0];

            // 可选：如果 BinCount 未同步，进行修正（以 BinData/Blocks 为准更安全）
            // 这里选择：若 BinBlocks 有值，以它为准；否则保留 BinCount。
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
            return $"DefectList: {DefectListString}, FontMeasureSize: {FontMeasureSize}, " +
                   $"ImageUnitPixelActualSize: {ImageUnitPixelActualSize}, DrawLineWidth: {DrawLineWidth}, TimedReplyOK: {TimedReplyOK}";
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

            // BitConverter 依赖系统端序；Windows/.NET 通常 LE，但工业建议显式拼
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
