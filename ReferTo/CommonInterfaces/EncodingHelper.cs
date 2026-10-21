using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class EncodingHelper
    {
        public static bool IsValidEncoding(byte[] bytes, Encoding encoding)
        {
            try
            {
                // 尝试解码并重新编码
                string decodedString = encoding.GetString(bytes);
                byte[] encodedBytes = encoding.GetBytes(decodedString);
                return bytes.Length == encodedBytes.Length && bytes.SequenceEqual(encodedBytes);
            }
            catch
            {
                // 如果解码失败，返回false
                return false;
            }
        }

        public static bool IsValidEncoding(string[] strings, Encoding encoding)
        {
            try
            {
                foreach (var str in strings)
                {
                    byte[] encodedBytes = encoding.GetBytes(str); // 将字符串编码为字节
                    string decodedString = encoding.GetString(encodedBytes); // 将字节解码回字符串
                    if (str != decodedString)
                    { // 比较原始字符串和解码后的字符串
                        return false; // 如果有任何不匹配，返回false
                    }
                }
                return true; // 所有字符串都有效地编解码
            }
            catch (Exception)
            {
                // 如果解码失败，返回false
                return false;
            }
        }

        public static Encoding GetEncoding(string filePath)
        {
            byte[] buffer = new byte[3];
            using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                file.Read(buffer, 0, 3);
            }

            if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return Encoding.UTF8;  // UTF-8 BOM
            }
            else
            {
                return null;  // No BOM found
            }
        }

        public static Encoding GuessFileEncoding(string filePath)
        {
            byte[] fileContent = File.ReadAllBytes(filePath);
            return GuessFileEncoding(fileContent);
        }

        public static Encoding GuessFileEncoding(byte[] fileContent)
        {
            if (fileContent != null && fileContent.Length >= 3 && fileContent[0] == 0xEF && fileContent[1] == 0xBB && fileContent[2] == 0xBF)
            {
                return Encoding.UTF8;  // UTF-8 BOM
            }
            if (IsValidEncoding(fileContent, Encoding.UTF8))
            {
                return Encoding.UTF8;
            }
            else if (IsValidEncoding(fileContent, Encoding.GetEncoding("GBK")))
            {
                return Encoding.GetEncoding("GBK");
            }
            return null;
        }

        // 调用者自己处理异常
        public static byte[] GbkToUtf8(byte[] gbkBytes)
        {
            // 创建GBK编码器
            Encoding gbk = Encoding.GetEncoding("GBK");

            // 将GBK字节数组解码为字符串
            string unicodeString = gbk.GetString(gbkBytes);

            // 将字符串编码为UTF-8字节数组
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(unicodeString);

            return utf8Bytes;
        }

        // 调用者自己处理异常
        public static byte[] Utf8ToGbk(byte[] utf8Bytes)
        {
            // 解码UTF-8字节数组为字符串
            string unicodeString = Encoding.UTF8.GetString(utf8Bytes);

            // 创建GBK编码器
            Encoding gbk = Encoding.GetEncoding("GBK");

            // 将字符串编码为GBK字节数组
            byte[] gbkBytes = gbk.GetBytes(unicodeString);

            return gbkBytes;
        }

        // 调用者自己处理异常
        public static string[] ConvertArrayEncoding(string[] inputStrings, string fromEncoding, string toEncoding)
        {
            Encoding sourceEncoding = Encoding.GetEncoding(fromEncoding);
            Encoding targetEncoding = Encoding.GetEncoding(toEncoding);

            string[] outputStrings = new string[inputStrings.Length];
            for (int i = 0; i < inputStrings.Length; i++)
            {
                byte[] sourceBytes = sourceEncoding.GetBytes(inputStrings[i]);
                byte[] targetBytes = Encoding.Convert(sourceEncoding, targetEncoding, sourceBytes);
                outputStrings[i] = targetEncoding.GetString(targetBytes);
            }

            return outputStrings;
        }
    }

    public static class EncodingTestor
    {
        public static void HelloIsValidEncoding()
        {
            byte[] bytes = { /* byte array data */ };

            // 尝试用UTF-8解码
            if (EncodingHelper.IsValidEncoding(bytes, Encoding.UTF8))
            {
                Console.WriteLine("Data is likely UTF-8 encoded.");
            }
            else if (EncodingHelper.IsValidEncoding(bytes, Encoding.GetEncoding("GBK")))
            {
                Console.WriteLine("Data is likely GBK encoded.");
            }
            else
            {
                Console.WriteLine("Unknown encoding.");
            }
        }

        public static void HelloIsValidEncoding2()
        {
            string[] strings = { "你好", "世界", "Hello", "World" }; // 一些示例字符串

            // 检查字符串数组是否可以在UTF-8编码下有效编解码
            if (EncodingHelper.IsValidEncoding(strings, Encoding.UTF8))
            {
                Console.WriteLine("All strings are valid under UTF-8 encoding.");
            }
            else
            {
                Console.WriteLine("Some strings are invalid under UTF-8 encoding.");
            }

            // 检查字符串数组是否可以在GBK编码下有效编解码
            if (EncodingHelper.IsValidEncoding(strings, Encoding.GetEncoding("GBK")))
            {
                Console.WriteLine("All strings are valid under GBK encoding.");
            }
            else
            {
                Console.WriteLine("Some strings are invalid under GBK encoding.");
            }
        }

        public static void HelloGbkToUtf8()
        {
            // 假定这是一个GBK编码的字节数组
            byte[] gbkBytes = { 0xC4, 0xE3, 0xBA, 0xC3 }; // "你好" in GBK

            try
            {
                // 转换为UTF-8字节数组
                byte[] utf8Bytes = EncodingHelper.GbkToUtf8(gbkBytes);

                // 输出UTF-8编码的字节数组
                Console.WriteLine("UTF-8 Bytes: " + BitConverter.ToString(utf8Bytes));

                // 为了验证，将UTF-8字节解码为字符串并输出
                string utf8String = Encoding.UTF8.GetString(utf8Bytes);
                Console.WriteLine("Decoded String: " + utf8String);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
           
        }

        public static void helloConvertArrayEncoding()
        {
            // 假设这些是GBK编码的字符串数组
            string[] utf8Strings = { "你好", "世界" };
            string[] gbkStrings = { "Hello", "World" };

            try
            {
                // 将GBK编码的字符串数组转换为UTF-8编码的字符串数组
                string[] convertedToUtf8 = EncodingHelper.ConvertArrayEncoding(gbkStrings, "GBK", "UTF-8");

                // 将UTF-8编码的字符串数组转换为GBK编码的字符串数组
                string[] convertedToGbk = EncodingHelper.ConvertArrayEncoding(utf8Strings, "UTF-8", "GBK");

                // 输出转换后的UTF-8字符串
                Console.WriteLine("Converted to UTF-8:");
                foreach (var str in convertedToUtf8)
                {
                    Console.WriteLine(str);
                }

                // 输出转换后的GBK字符串
                Console.WriteLine("Converted to GBK:");
                foreach (var str in convertedToGbk)
                {
                    Console.WriteLine(str);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
           
        }
    }
}
