using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class StringHelper
    {
        public static string ExtractShortNameAfterDot(string fullName)
        {
            // 分割字符串，并获取最后一个部分
            var parts = fullName.Split('.');
            return parts.Length > 1 ? parts[parts.Length - 1] : fullName;
        }

        public static List<string> ExtractDirName(List<string> directoryList)
        {
            List<string> dirNameList = new List<string>();
            for (int i = 0; i < directoryList.Count; i++)
            {
                string fullPath = directoryList[i];
                dirNameList.Add(fullPath.Substring(fullPath.LastIndexOf(Path.DirectorySeparatorChar) + 1));
            }
            return directoryList;
        }

        public static string BytesToHexString(byte[] value)
        {
            StringBuilder hex = new StringBuilder(value.Length * 2);
            foreach (byte b in value)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        public static string BytesToHexString(byte[] value, int startIndex, int length)
        {
            if (value == null)
            {
                //throw new ArgumentNullException("value");
                return string.Empty;
            }

            if (startIndex < 0 || (startIndex >= value.Length && startIndex > 0))
            {
                //throw new ArgumentOutOfRangeException("startIndex", Environment.GetResourceString("ArgumentOutOfRange_StartIndex"));
                return string.Empty;
            }

            if (length < 0)
            {
                //throw new ArgumentOutOfRangeException("length", Environment.GetResourceString("ArgumentOutOfRange_GenericPositive"));
                return string.Empty;
            }

            if (startIndex > value.Length - length)
            {
                //throw new ArgumentException(Environment.GetResourceString("Arg_ArrayPlusOffTooSmall"));
                return string.Empty;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            if (length > 715827882)
            {
                //throw new ArgumentOutOfRangeException("length", Environment.GetResourceString("ArgumentOutOfRange_LengthTooLarge", 715827882));
                return string.Empty;
            }

            StringBuilder hex = new StringBuilder(value.Length * 2);
            int num = startIndex + length;
            for (int i = startIndex; i < num; i++)
            {
                hex.AppendFormat("{0:x2} ", value[i]);
            }
            return hex.ToString();
        }

        public static byte[] HexStringToBytes(string hexString)
        {
            if (!string.IsNullOrEmpty(hexString)) return new byte[0];
            // 如果有空格，去掉
            hexString = hexString.Replace(" ", "");
            if (!string.IsNullOrEmpty(hexString) && hexString.Length % 2 == 0)
            {
                try
                {
                    byte[] bytes = new byte[hexString.Length / 2];
                    for (int i = 0; i < hexString.Length; i += 2)
                    {
                        string hexByte = hexString.Substring(i, 2);
                        bytes[i / 2] = Convert.ToByte(hexByte, 16);
                    }
                    return bytes;
                }
                catch (Exception)
                {
                }
            }
            return new byte[0];
        }

        public static string ReplaceFirstFromRight(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);
            if (place == -1)
            {
                return source; // 如果没有找到需要替换的字符串，返回原字符串
            }

            string result = source.Remove(place, find.Length).Insert(place, replace);
            return result;
        }

        public static string ReplaceFirstFromRight(string source, string[] findArray, string replace)
        {
            int place = -1;
            string find = null;

            foreach (var item in findArray)
            {
                int tempPlace = source.LastIndexOf(item);
                if (tempPlace > place)
                {
                    place = tempPlace;
                    find = item;
                }
            }

            if (place == -1 || find == null)
            {
                return source; // 如果没有找到需要替换的字符串，返回原字符串
            }

            string result = source.Remove(place, find.Length).Insert(place, replace);
            return result;
        }

        public static string Reverse(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }
            char[] charArray = source.ToCharArray();
            Array.Reverse(charArray);
            string reversed = new string(charArray);
            return reversed;
        }

        public static string ToString(double v)
        {
            return v.ToString("0.#####", CultureInfo.InvariantCulture);
        }

    }

}
