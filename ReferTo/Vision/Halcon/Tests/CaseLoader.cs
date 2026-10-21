using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule.Tests
{
    // 加载 Cases/*.json

    public static class CaseLoader
    {
        public static Dictionary<string, List<CaseItem>> LoadAll(string casesDir)
        {
            var map = new Dictionary<string, List<CaseItem>>();

            foreach (var file in Directory.GetFiles(casesDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                var json = File.ReadAllText(file);
                var obj = JsonConvert.DeserializeObject<OpCasesFile>(json);

                if (obj == null || string.IsNullOrEmpty(obj.op) || obj.cases == null) continue;
                map[obj.op] = obj.cases;
            }

            return map;
        }
    }

    // Case 数据模型

    public sealed class OpCasesFile
    {
        public string op { get; set; }
        public List<CaseItem> cases { get; set; }
    }

    public sealed class CaseItem
    {
        public string caseId { get; set; }

        // inputs: key -> value (JToken，后面 builder 解析)
        public Dictionary<string, JToken> inputs { get; set; }

        // 覆盖输出断言： key -> {min,max,length,oneOf...}
        public Dictionary<string, JObject> expectOverrides { get; set; }

        // 让后续 Case / preStep 当作输入用
        public Dictionary<string, string> export;

        public Perf perf { get; set; }

        // 前置步骤：用于依赖（例如先 initialize_config，再跑 image_preproces）
        public List<PreStep> preSteps { get; set; }
    }

    // export = 当前 Case 把 outDict 里的某些 Key，
    //     导出到 sharedContext，
    //     让后续 Case / preStep 当作输入用
    public sealed class PreStep
    {
        public string op { get; set; }
        public Dictionary<string, JToken> inputs { get; set; }

        //// 导出：把 outDict 的某 key 存入共享上下文
        //// 例如 { "ConfigDict": "ConfigDict" }
        //public Dictionary<string, string> export { get; set; }

        // 将 export 改为 Dictionary<string, object> 以支持混合类型
        [JsonProperty("export")]
        public Dictionary<string, object> ExportMixed { get; set; }


        // 添加一个属性来方便访问（保持原有接口）
        [JsonIgnore]
        public Dictionary<string, string> export
        {
            get
            {
                if (ExportMixed == null) return null;

                var result = new Dictionary<string, string>();
                foreach (var kv in ExportMixed)
                {
                    if (kv.Value is string strValue)
                    {
                        result[kv.Key] = strValue;
                    }
                    else if (kv.Value is JObject jObj)
                    {
                        // 如果是对象格式，提取 name 字段
                        string name = jObj["name"]?.ToString() ?? kv.Key;
                        result[kv.Key] = name;
                    }
                    else
                    {
                        result[kv.Key] = kv.Value?.ToString() ?? kv.Key;
                    }
                }
                return result;
            }
        }

        // 添加方法获取导出项的类型信息
        public string GetExportKind(string key)
        {
            if (ExportMixed == null || !ExportMixed.ContainsKey(key))
                return null;

            var value = ExportMixed[key];
            if (value is JObject jObj)
            {
                return jObj["kind"]?.ToString();
            }

            // 如果是字符串格式，根据名称推断
            if (value is string strValue)
            {
                return InferKindFromName(key);
            }

            return null;
        }

        private string InferKindFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "object";

            string nameLower = name.ToLower();
            if (nameLower.Contains("image")) return "image";
            if (nameLower.Contains("region") || nameLower.Contains("border")) return "region";
            if (nameLower.Contains("dict") || nameLower.EndsWith("ary")) return "dict";
            return "object";
        }
    }

    public class ExportItem
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }
    }

    public sealed class Perf
    {
        public int? maxMs { get; set; }
    }

}
