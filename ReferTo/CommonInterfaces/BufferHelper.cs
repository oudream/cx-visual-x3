using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class BufferHelper
    {
        public static int ConvertBigEndianToInt(byte[] buf, bool isLittleEndian = true)
        {
            if (isLittleEndian)
            {
                return BinaryPrimitives.ReadInt32LittleEndian(buf);
            }
            else
            {
                return BinaryPrimitives.ReadInt32BigEndian(buf);
            }
            //if (BitConverter.IsLittleEndian)
            //{
            //    Array.Reverse(buf);
            //}

            ////BinaryPrimitives.ReadInt16BigEndian(bigEndianBytes);
            //return BinaryPrimitives.ReadInt32BigEndian(buf);
            //return BitConverter.ToInt32(buf, 0);
        }

        /// <summary>
        ///     Converts an array of bytes to an ASCII byte array.
        /// </summary>
        /// <param name="numbers">The byte array.</param>
        /// <returns>An array of ASCII byte values.</returns>
        public static byte[] GetAsciiBytes(params byte[] numbers)
        {
            return Encoding.UTF8.GetBytes(numbers.SelectMany(n => n.ToString("X2")).ToArray());
        }

        /// <summary>
        ///     Converts a hex string to a byte array.
        /// </summary>
        /// <param name="hex">The hex string.</param>
        /// <returns>Array of bytes.</returns>
        public static byte[] HexToBytes(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            if (hex.Length % 2 != 0)
            {
                throw new FormatException("Hex string must have even number of characters.");
            }

            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        public static string BytesToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        /// <summary>把字节数转换成人类友好的 KB/MB/GB</summary>
        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:0.##}{units[unitIndex]}";
        }
    }
}
