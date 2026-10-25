using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CxVisual.Foundation.Data.Commands
{
    /// <summary>
    /// 跨进程Command值层版本1：显式白名单类型+有界帧；不使用BinaryFormatter或任意CLR类型加载。
    /// 仅定义可安全传输的值，不提供网络、身份认证、设备执行授权或对象句柄所有权。
    /// </summary>
    public static class FDCCommandValueCodec
    {
        private const int Magic = 0x31444D43; // "CMD1" little-endian
        public const int MaxFrameBytes = 4 * 1024 * 1024;
        private const int MaxPayloadBytes = 2 * 1024 * 1024;
        private const int MaxTextBytes = 65536;
        private const int MaxItems = 10000;
        private const int MaxDepth = 16;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        private enum WireType : byte
        {
            Null, Boolean, Int32, Int64, Double, String, Bytes,
            ObjectMap, ObjectList, Decimal, Single, Guid, DateTime,
            Int16, Byte, UInt32, UInt64, UInt16, SByte, TypedArray
        }

        /// <summary>使用唯一安全格式编码根命名对象；拒绝DTO、委托、Native指针、循环与超大数据。</summary>
        public static byte[] Encode(IReadOnlyDictionary<string, object> values)
        {
            if (values == null) throw new ArgumentNullException("values");
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Utf8, true))
            {
                writer.Write(Magic);
                writer.Write((byte)1);
                // 编码前先经统一值安全机制冻结输入，避免并发改写期间的非一致帧。
                WriteMap(writer, FDCCommandValueSnapshot.Capture(values), 0);
                writer.Flush();
                if (stream.Length > MaxFrameBytes)
                    throw new ArgumentException("Command值帧超过4MiB安全上限", "values");
                return stream.ToArray();
            }
        }

        /// <summary>严格读取单帧并验证版本、长度、类型、嵌套与尾随字节。</summary>
        public static IReadOnlyDictionary<string, object> Decode(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (bytes.Length > MaxFrameBytes || bytes.Length < 9)
                throw new InvalidDataException("命令值帧长度无效");
            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream, Utf8, true))
            {
                if (reader.ReadInt32() != Magic || reader.ReadByte() != 1)
                    throw new InvalidDataException("命令值帧版本或标识无效");
                Dictionary<string, object> values = ReadMap(reader, 0);
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("命令值帧存在尾随字节");
                return FDCCommandValueSnapshot.Capture(values);
            }
        }

        private static void CheckDepth(int depth)
        {
            if (depth > MaxDepth) throw new InvalidDataException("命令值嵌套超过16层");
        }
        private static void CheckCount(int count)
        {
            if (count < 0 || count > MaxItems)
                throw new InvalidDataException("命令值集合容量无效");
        }
        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            byte[] bytes = Utf8.GetBytes(value);
            if (bytes.Length > MaxTextBytes)
                throw new ArgumentException("Command字符串超过64KiB安全上限");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
        private static string ReadString(BinaryReader reader)
        {
            int size = reader.ReadInt32();
            if (size < 0 || size > MaxTextBytes)
                throw new InvalidDataException("命令字符串长度无效");
            byte[] bytes = reader.ReadBytes(size);
            if (bytes.Length != size) throw new EndOfStreamException("命令字符串不完整");
            return Utf8.GetString(bytes);
        }
        private static void WriteMap(BinaryWriter writer,
            IReadOnlyDictionary<string, object> map, int depth)
        {
            CheckDepth(depth);
            CheckCount(map.Count);
            writer.Write(map.Count);
            // 编码顺序稳定，不依赖Dictionary插入次序。
            foreach (KeyValuePair<string, object> item in map.OrderBy(
                x => x.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                    throw new ArgumentException("命令参数键不能为空");
                WriteString(writer, item.Key);
                WriteValue(writer, item.Value, depth + 1);
            }
        }
        private static Dictionary<string, object> ReadMap(BinaryReader reader, int depth)
        {
            CheckDepth(depth);
            int count = reader.ReadInt32();
            CheckCount(count);
            Dictionary<string, object> result =
                new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string key = ReadString(reader);
                if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key))
                    throw new InvalidDataException("命令参数键无效或重复");
                result.Add(key, ReadValue(reader, depth + 1));
            }
            return result;
        }
        private static WireType ElementType(Type type)
        {
            if (type == typeof(object)) return WireType.Null;
            if (type == typeof(bool)) return WireType.Boolean;
            if (type == typeof(int)) return WireType.Int32;
            if (type == typeof(long)) return WireType.Int64;
            if (type == typeof(double)) return WireType.Double;
            if (type == typeof(string)) return WireType.String;
            if (type == typeof(decimal)) return WireType.Decimal;
            if (type == typeof(float)) return WireType.Single;
            if (type == typeof(Guid)) return WireType.Guid;
            if (type == typeof(DateTime)) return WireType.DateTime;
            if (type == typeof(short)) return WireType.Int16;
            if (type == typeof(byte)) return WireType.Byte;
            if (type == typeof(uint)) return WireType.UInt32;
            if (type == typeof(ulong)) return WireType.UInt64;
            if (type == typeof(ushort)) return WireType.UInt16;
            if (type == typeof(sbyte)) return WireType.SByte;
            throw new NotSupportedException("命令远程数组元素类型不在安全白名单：" +
                type.FullName);
        }
        private static Type ElementClrType(WireType tag)
        {
            switch (tag)
            {
                case WireType.Null: return typeof(object);
                case WireType.Boolean: return typeof(bool);
                case WireType.Int32: return typeof(int);
                case WireType.Int64: return typeof(long);
                case WireType.Double: return typeof(double);
                case WireType.String: return typeof(string);
                case WireType.Decimal: return typeof(decimal);
                case WireType.Single: return typeof(float);
                case WireType.Guid: return typeof(Guid);
                case WireType.DateTime: return typeof(DateTime);
                case WireType.Int16: return typeof(short);
                case WireType.Byte: return typeof(byte);
                case WireType.UInt32: return typeof(uint);
                case WireType.UInt64: return typeof(ulong);
                case WireType.UInt16: return typeof(ushort);
                case WireType.SByte: return typeof(sbyte);
                default: throw new InvalidDataException("未知远程数组元素类型");
            }
        }
        private static void WriteValue(BinaryWriter writer, object value, int depth)
        {
            CheckDepth(depth);
            if (value == null) { writer.Write((byte)WireType.Null); return; }
            if (value is bool) { writer.Write((byte)WireType.Boolean); writer.Write((bool)value); return; }
            if (value is int) { writer.Write((byte)WireType.Int32); writer.Write((int)value); return; }
            if (value is long) { writer.Write((byte)WireType.Int64); writer.Write((long)value); return; }
            if (value is double) { writer.Write((byte)WireType.Double); writer.Write((double)value); return; }
            if (value is string) { writer.Write((byte)WireType.String); WriteString(writer, (string)value); return; }
            byte[] bytes = value as byte[];
            if (bytes != null)
            {
                if (bytes.Length > MaxPayloadBytes)
                    throw new ArgumentException("命令二进制值超过2MiB安全上限");
                writer.Write((byte)WireType.Bytes);
                writer.Write(bytes.Length);
                writer.Write(bytes);
                return;
            }
            if (value is decimal) { writer.Write((byte)WireType.Decimal); writer.Write((decimal)value); return; }
            if (value is float) { writer.Write((byte)WireType.Single); writer.Write((float)value); return; }
            if (value is Guid) { writer.Write((byte)WireType.Guid); writer.Write(((Guid)value).ToByteArray()); return; }
            if (value is DateTime) { writer.Write((byte)WireType.DateTime); writer.Write(((DateTime)value).ToBinary()); return; }
            if (value is short) { writer.Write((byte)WireType.Int16); writer.Write((short)value); return; }
            if (value is byte) { writer.Write((byte)WireType.Byte); writer.Write((byte)value); return; }
            if (value is uint) { writer.Write((byte)WireType.UInt32); writer.Write((uint)value); return; }
            if (value is ulong) { writer.Write((byte)WireType.UInt64); writer.Write((ulong)value); return; }
            if (value is ushort) { writer.Write((byte)WireType.UInt16); writer.Write((ushort)value); return; }
            if (value is sbyte) { writer.Write((byte)WireType.SByte); writer.Write((sbyte)value); return; }
            IReadOnlyDictionary<string, object> map =
                value as IReadOnlyDictionary<string, object>;
            if (map != null)
            {
                writer.Write((byte)WireType.ObjectMap);
                WriteMap(writer, map, depth + 1);
                return;
            }
            Array array = value as Array;
            if (array != null)
            {
                if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                    throw new NotSupportedException("命令仅支持零基一维数组");
                CheckCount(array.Length);
                WireType element = ElementType(array.GetType().GetElementType());
                writer.Write((byte)WireType.TypedArray);
                writer.Write((byte)element);
                writer.Write(array.Length);
                foreach (object item in array) WriteValue(writer, item, depth + 1);
                return;
            }
            IReadOnlyList<object> list = value as IReadOnlyList<object>;
            if (list != null)
            {
                CheckCount(list.Count);
                writer.Write((byte)WireType.ObjectList);
                writer.Write(list.Count);
                foreach (object item in list) WriteValue(writer, item, depth + 1);
                return;
            }
            throw new NotSupportedException(
                "不允许对未登记DTO、Enum、Native句柄或任意CLR对象进行命令反序列化：" +
                value.GetType().FullName);
        }
        private static object ReadValue(BinaryReader reader, int depth)
        {
            CheckDepth(depth);
            WireType tag = (WireType)reader.ReadByte();
            switch (tag)
            {
                case WireType.Null: return null;
                case WireType.Boolean: return reader.ReadBoolean();
                case WireType.Int32: return reader.ReadInt32();
                case WireType.Int64: return reader.ReadInt64();
                case WireType.Double: return reader.ReadDouble();
                case WireType.String: return ReadString(reader);
                case WireType.Bytes:
                    {
                        int size = reader.ReadInt32();
                        if (size < 0 || size > MaxPayloadBytes)
                            throw new InvalidDataException("二进制值长度无效");
                        byte[] buffer = reader.ReadBytes(size);
                        if (buffer.Length != size) throw new EndOfStreamException("二进制值不完整");
                        return buffer;
                    }
                case WireType.Decimal: return reader.ReadDecimal();
                case WireType.Single: return reader.ReadSingle();
                case WireType.Guid:
                    {
                        byte[] guid = reader.ReadBytes(16);
                        if (guid.Length != 16) throw new EndOfStreamException("Guid不完整");
                        return new Guid(guid);
                    }
                case WireType.DateTime: return DateTime.FromBinary(reader.ReadInt64());
                case WireType.Int16: return reader.ReadInt16();
                case WireType.Byte: return reader.ReadByte();
                case WireType.UInt32: return reader.ReadUInt32();
                case WireType.UInt64: return reader.ReadUInt64();
                case WireType.UInt16: return reader.ReadUInt16();
                case WireType.SByte: return reader.ReadSByte();
                case WireType.ObjectMap: return ReadMap(reader, depth + 1);
                case WireType.ObjectList:
                    {
                        int count = reader.ReadInt32();
                        CheckCount(count);
                        List<object> items = new List<object>(count);
                        for (int i = 0; i < count; i++) items.Add(ReadValue(reader, depth + 1));
                        return items;
                    }
                case WireType.TypedArray:
                    {
                        WireType typeTag = (WireType)reader.ReadByte();
                        Type element = ElementClrType(typeTag);
                        int count = reader.ReadInt32();
                        CheckCount(count);
                        Array items = Array.CreateInstance(element, count);
                        for (int i = 0; i < count; i++)
                        {
                            object value = ReadValue(reader, depth + 1);
                            if (value != null && !element.IsInstanceOfType(value))
                                throw new InvalidDataException("数组元素实际类型与声明类型不一致");
                            if (value == null && element.IsValueType)
                                throw new InvalidDataException("数组中非空值类型元素不能为null");
                            items.SetValue(value, i);
                        }
                        return items;
                    }
                default: throw new InvalidDataException("命令值Tag未定义");
            }
        }
    }
}
