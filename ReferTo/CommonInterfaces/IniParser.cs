using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    // 一个解析INI文件类，能解析这文件包括注释(//后的内容)（string section, string key, string value）
    // 能查询是否存在（section、key）、读值、更新值，能保存这文件（带上注释）。
    public class IniParser
    {
        // 定义每一行的类型
        public enum LineType { Blank, Comment, Section, KeyValue }

        // 定义表示文件中一行的结构
        public class Line
        {
            public LineType Type;
            public string Raw;          // 原始文本（备用，可用于调试）
            public string SectionName;  // 如果是 section 行，则存储 section 名称
            public string Key;          // 如果是键值行，则存储 key
            public string Value;        // 如果是键值行，则存储 value
            public string Comment;      // 整行注释或键值行内的注释
        }

        // 保存文件中所有行
        private List<Line> lines = new List<Line>();

        /// <summary>
        /// 从文件加载 INI 配置
        /// </summary>
        public bool LoadFromFile(string filename)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine("文件不存在：" + filename);
                return false;
            }

            lines.Clear();
            string currentSection = "";
            foreach (string line in File.ReadAllLines(filename))
            {
                Line parsedLine = ParseLine(line);
                lines.Add(parsedLine);

                // 如果遇到 section 行，更新当前 section（后续键值行属于该 section）
                if (parsedLine.Type == LineType.Section)
                {
                    currentSection = parsedLine.SectionName;
                }
            }
            return true;
        }

        /// <summary>
        /// 保存 INI 配置到文件，保存时会保留注释和空行
        /// </summary>
        public bool SaveToFile(string filename)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filename))
                {
                    foreach (var line in lines)
                    {
                        writer.WriteLine(ComposeLine(line));
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("写入文件失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 判断指定 section 是否存在
        /// </summary>
        public bool HasSection(string section)
        {
            foreach (var line in lines)
            {
                if (line.Type == LineType.Section && line.SectionName == section)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断指定 section 中是否存在某个 key
        /// </summary>
        public bool HasKey(string section, string key)
        {
            bool inSection = false;
            foreach (var line in lines)
            {
                if (line.Type == LineType.Section)
                {
                    inSection = (line.SectionName == section);
                }
                else if (inSection && line.Type == LineType.KeyValue)
                {
                    if (line.Key == key)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取指定 section 下 key 的值，若不存在则返回空字符串
        /// </summary>
        public string GetValue(string section, string key)
        {
            bool inSection = false;
            foreach (var line in lines)
            {
                if (line.Type == LineType.Section)
                    inSection = (line.SectionName == section);
                else if (inSection && line.Type == LineType.KeyValue)
                {
                    if (line.Key == key)
                        return line.Value;
                }
            }
            return "";
        }

        /// <summary>
        /// 设置指定 section 下 key 的值，若不存在则自动添加（若 section 不存在，则先添加 section）
        /// </summary>
        public void SetValue(string section, string key, string value)
        {
            bool sectionFound = false;
            bool inSection = false;
            int insertPos = lines.Count; // 记录插入位置

            // 遍历文件中所有行，查找指定 section 和 key
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Type == LineType.Section)
                {
                    if (line.SectionName == section)
                    {
                        sectionFound = true;
                        inSection = true;
                        // 记录 section 行后第一个可插入的位置
                        insertPos = i + 1;
                    }
                    else
                    {
                        inSection = false;
                    }
                }
                else if (inSection && line.Type == LineType.KeyValue)
                {
                    if (line.Key == key)
                    {
                        // 找到则直接更新 value
                        line.Value = value;
                        return;
                    }
                    // 更新插入位置到当前行之后
                    insertPos = i + 1;
                }
            }

            // 如果 section 存在但未找到 key，则在该 section 末尾插入新的键值行
            if (sectionFound)
            {
                Line newLine = new Line
                {
                    Type = LineType.KeyValue,
                    Key = key,
                    Value = value
                };
                lines.Insert(insertPos, newLine);
            }
            else
            {
                // 如果 section 不存在，则先添加 section，再添加键值行
                Line secLine = new Line
                {
                    Type = LineType.Section,
                    SectionName = section
                };
                lines.Add(secLine);
                Line newLine = new Line
                {
                    Type = LineType.KeyValue,
                    Key = key,
                    Value = value
                };
                lines.Add(newLine);
            }
        }

        /// <summary>
        /// 解析单行文本，判断行类型并提取 section/key/value 和注释
        /// </summary>
        private Line ParseLine(string line)
        {
            Line result = new Line();
            result.Raw = line;
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                result.Type = LineType.Blank;
                return result;
            }
            // 如果整行是注释（以 ";" 或 "//" 开头）
            if (trimmed.StartsWith(";") || trimmed.StartsWith("//"))
            {
                result.Type = LineType.Comment;
                result.Comment = trimmed;
                return result;
            }
            // 如果以 [ 开头并以 ] 结尾，则为 section 行
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                result.Type = LineType.Section;
                result.SectionName = trimmed.Substring(1, trimmed.Length - 2).Trim();
                return result;
            }
            // 否则，尝试以第一个 "=" 分隔解析为 key=value
            int pos = trimmed.IndexOf('=');
            if (pos >= 0)
            {
                result.Type = LineType.KeyValue;
                result.Key = trimmed.Substring(0, pos).Trim();
                string remainder = trimmed.Substring(pos + 1).Trim();
                // 检查是否存在内嵌注释（以"//"为标记）
                int commentPos = remainder.IndexOf("//");
                if (commentPos >= 0)
                {
                    result.Value = remainder.Substring(0, commentPos).Trim();
                    result.Comment = remainder.Substring(commentPos).Trim();
                }
                else
                {
                    result.Value = remainder;
                }
                return result;
            }
            // 如果都不匹配，则当作注释行处理
            result.Type = LineType.Comment;
            result.Comment = trimmed;
            return result;
        }

        /// <summary>
        /// 根据行类型重新组合成字符串，用于写回文件
        /// </summary>
        private string ComposeLine(Line line)
        {
            switch (line.Type)
            {
                case LineType.Blank:
                    return "";
                case LineType.Comment:
                    return line.Comment;
                case LineType.Section:
                    return $"[{line.SectionName}]";
                case LineType.KeyValue:
                    {
                        string composed = $"{line.Key}={line.Value}";
                        if (!string.IsNullOrEmpty(line.Comment))
                        {
                            // 如果内嵌注释未以 "//" 或 ";" 开头，则自动添加 "//"
                            if (!(line.Comment.StartsWith("//") || line.Comment.StartsWith(";")))
                                composed += " //" + line.Comment;
                            else
                                composed += " " + line.Comment;
                        }
                        return composed;
                    }
                default:
                    return line.Raw;
            }
        }

        // ===== 使用示例 =====
        public static void Test(string filePath)
        {
            IniParser parser = new IniParser();

            // 加载 INI 文件
            if (!parser.LoadFromFile(filePath))
            {
                Console.WriteLine("加载 INI 文件失败！");
                return;
            }

            // 查询指定 section/key 是否存在
            if (parser.HasSection("Settings"))
            {
                Console.WriteLine("[Settings] 节存在。");
            }
            if (parser.HasKey("Settings", "ProductType"))
            {
                Console.WriteLine("ProductType = " + parser.GetValue("Settings", "ProductType"));
            }

            // 更新键值，如果键不存在则会自动添加
            parser.SetValue("Settings", "ProductType", "v54");

            // 添加新的 section 和键值
            parser.SetValue("NewSection", "NewKey", "NewValue");

            // 保存更新后的文件，保留原有注释信息
            if (!parser.SaveToFile("config_updated.ini"))
            {
                Console.WriteLine("保存 INI 文件失败！");
            }
            else
            {
                Console.WriteLine("更新后的 INI 文件已保存为 config_updated.ini");
            }
        }
    }

}
