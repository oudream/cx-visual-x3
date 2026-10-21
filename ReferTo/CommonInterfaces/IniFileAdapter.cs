using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public class IniFileAdapter
    {
        private string _filePath;

        public IniFileAdapter(string filePath)
        {
            _filePath = filePath;
        }

        public static string GetValue(Dictionary<string, Dictionary<string, string>> data, string section, string key, string defaultValue = "")
        {
            try
            {
                if (data.ContainsKey(section) && data[section].ContainsKey(key))
                    return data[section][key];

                return defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取INI文件时出错: {ex.Message}");
                return defaultValue;
            }
        }

        public static void SetValues(Dictionary<string, Dictionary<string, string>> data, string section, Dictionary<string, string> values)
        {
            try
            {
                if (!data.ContainsKey(section))
                    data[section] = new Dictionary<string, string>();

                foreach (var pair in values)
                {
                    data[section][pair.Key] = pair.Value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置多个键值对时出错: {ex.Message}");
            }
        }

        public static void SetValue(Dictionary<string, Dictionary<string, string>> data, string section, string key, string value)
        {
            try
            {
                if (!data.ContainsKey(section))
                    data[section] = new Dictionary<string, string>();

                data[section][key] = value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置多个键值对时出错: {ex.Message}");
            }
        }

        // 写入单个键值对
        public void WriteValue(string section, string key, string value)
        {
            try
            {
                var data = ReadIniFile();
                if (!data.ContainsKey(section))
                    data[section] = new Dictionary<string, string>();

                data[section][key] = value;
                WriteIniFile(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入INI文件时出错: {ex.Message}");
            }
        }

        // 一次写入多个键值对
        public void WriteValues(string section, Dictionary<string, string> values)
        {
            try
            {
                var data = ReadIniFile();
                if (!data.ContainsKey(section))
                    data[section] = new Dictionary<string, string>();

                foreach (var pair in values)
                {
                    data[section][pair.Key] = pair.Value;
                }
                WriteIniFile(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入多个键值对时出错: {ex.Message}");
            }
        }

        // 读取单个键值对
        public string ReadValue(string section, string key, string defaultValue = "")
        {
            try
            {
                var data = ReadIniFile();
                if (data.ContainsKey(section) && data[section].ContainsKey(key))
                    return data[section][key];

                return defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取INI文件时出错: {ex.Message}");
                return defaultValue;
            }
        }

        // 读取整个节的键值对
        public Dictionary<string, Dictionary<string, string>> ReadIniFile()
        {
            var data = new Dictionary<string, Dictionary<string, string>>();
            try
            {
                if (!File.Exists(_filePath))
                    return data;

                Dictionary<string, string> currentSection = null;

                foreach (var line in File.ReadAllLines(_filePath))
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith(";")) continue; // 忽略注释
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        if (!data.ContainsKey(sectionName))
                        {
                            currentSection = new Dictionary<string, string>();
                            data[sectionName] = currentSection;
                        }
                    }
                    else if (currentSection != null)
                    {
                        var kvp = trimmedLine.Split(new[] { '=' }, 2);
                        if (kvp.Length == 2)
                            currentSection[kvp[0].Trim()] = kvp[1].Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解析INI文件时出错: {ex.Message}");
            }
            return data;
        }

        // 写入整个节的键值对
        public void WriteIniFile(Dictionary<string, Dictionary<string, string>> data)
        {
            try
            {
                using (var sw = new StreamWriter(_filePath))
                {
                    foreach (var section in data)
                    {
                        sw.WriteLine($"[{section.Key}]");
                        foreach (var kvp in section.Value)
                        {
                            sw.WriteLine($"{kvp.Key}={kvp.Value}");
                        }
                        sw.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入INI文件时出错: {ex.Message}");
            }
        }

        // 使用回调函数打印数据
        public static void PrintData(Dictionary<string, Dictionary<string, string>> data, Action<string> printMethod)
        {
            foreach (var section in data)
            {
                printMethod($"Section: [{section.Key}]");
                foreach (var keyValuePair in section.Value)
                {
                    printMethod($"  {keyValuePair.Key} = {keyValuePair.Value}");
                }
            }
        }

    }

}
