using HalconDotNet;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule.Tests
{
    // 把 case.inputs + attribute 输入定义 → HTuple dict

    public sealed class InDictBuilder
    {
        private readonly string _baseDir;
        public string BaseDir => _baseDir;

        public InDictBuilder(string baseDir)
        {
            _baseDir = baseDir;
        }

        public HTuple Build(HalconInAttribute[] inputs, Dictionary<string, JToken> caseInputs, Dictionary<string, object> sharedContext)
        {
            HTuple dict;
            HOperatorSet.CreateDict(out dict);

            foreach (var inp in inputs)
            {
                object value = null;

                if (inp.Source == HalconValueSource.SharedContext)
                {
                    if (string.IsNullOrEmpty(inp.ContextKey) || !sharedContext.TryGetValue(inp.ContextKey, out value))
                    {
                        if (inp.Required)
                            throw new Exception($"Missing SharedContext '{inp.ContextKey}' for input '{inp.Key}'");
                        continue;
                    }
                }
                else if (inp.Source == HalconValueSource.Constant)
                {
                    value = inp.Constant;
                }
                else
                {
                    if (caseInputs == null || !caseInputs.TryGetValue(inp.Key, out var token))
                    {
                        if (inp.Required) throw new Exception($"Missing case input '{inp.Key}'");
                        continue;
                    }
                    value = token;
                }

                SetDictValue(dict, inp.Key, inp.Kind, value);
            }

            return dict;
        }


        public object ResolveRawValue(HalconValueKind kind, object value)
        {
            // 复用 case 输入解析规则：image/int/double/string/tuple
            if (value is JToken jt)
            {
                // ========== 处理图像数组（特殊处理）==========
                if (jt.Type == JTokenType.Array && kind == HalconValueKind.Image)
                {
                    var jArray = (JArray)jt;

                    // 创建图像列表，不创建 HTuple
                    List<HObject> imageList = new List<HObject>();

                    foreach (var item in jArray)
                    {
                        if (item.Type == JTokenType.Object)
                        {
                            var jo = (JObject)item;
                            var type = jo.Value<string>("type");

                            if (string.Equals(type, "image", StringComparison.OrdinalIgnoreCase))
                            {
                                var path = jo.Value<string>("path");
                                var full = Path.IsPathRooted(path) ? path : Path.Combine(_baseDir, path);
                                HOperatorSet.ReadImage(out HObject img, full);
                                imageList.Add(img);
                            }
                        }
                    }

                    // 返回 List<HObject> 而不是 HTuple
                    return imageList;
                }

                // ========== 处理普通数组 ==========
                if (jt.Type == JTokenType.Array)
                {
                    var jArray = (JArray)jt;
                    HTuple result = new HTuple();

                    foreach (var item in jArray)
                    {
                        // 递归处理每个元素
                        var element = ResolveRawValue(kind, item);

                        if (element is HTuple ht)
                        {
                            // HTuple 合并
                            result = result.TupleConcat(ht);
                        }
                        else if (element is int || element is double || element is string || element is bool)
                        {
                            // 基本类型创建 HTuple 再合并
                            result = result.TupleConcat(new HTuple(element));
                        }
                        else if (element is HObject)
                        {
                            // 单个图像不应该出现在普通数组中，这是错误情况
                            throw new Exception("HObject should not appear in non-image array");
                        }
                        else
                        {
                            throw new Exception($"Unsupported element type in array: {element?.GetType()?.Name}");
                        }
                    }
                    return result;
                }

                if (jt.Type == JTokenType.Object)
                {
                    var jo = (JObject)jt;
                    var type = jo.Value<string>("type");

                    if (string.Equals(type, "image", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = jo.Value<string>("path");
                        var full = Path.IsPathRooted(path) ? path : Path.Combine(_baseDir, path);
                        HOperatorSet.ReadImage(out HObject img, full);
                        return img;  // 单个图像返回 HObject
                    }

                    if (string.Equals(type, "int", StringComparison.OrdinalIgnoreCase))
                        return new HTuple(jo.Value<int>("value"));

                    if (string.Equals(type, "double", StringComparison.OrdinalIgnoreCase))
                        return new HTuple(jo.Value<double>("value"));

                    if (string.Equals(type, "string", StringComparison.OrdinalIgnoreCase))
                        return new HTuple(jo.Value<string>("value"));

                    if (string.Equals(type, "tuple", StringComparison.OrdinalIgnoreCase))
                    {
                        var arr = jo["value"] as JArray;
                        var t = new HTuple();
                        if (arr != null)
                        {
                            foreach (var x in arr)
                                t = t.TupleConcat((x.Type == JTokenType.Float) ? new HTuple(x.Value<double>()) : new HTuple(x.Value<int>()));
                        }
                        return t;
                    }

                    throw new Exception($"Unsupported input object type '{type}'");
                }

                // 简单值
                if (jt.Type == JTokenType.Integer) return new HTuple(jt.Value<int>());
                if (jt.Type == JTokenType.Float) return new HTuple(jt.Value<double>());
                if (jt.Type == JTokenType.String) return new HTuple(jt.Value<string>());
            }

            // SharedContext/Constant 可能直接是 HTuple/HObject/primitive
            if (value is HTuple || value is HObject) return value;

            // 特殊处理：如果已经是 List<HObject>，直接返回
            if (value is List<HObject>) return value;

            // primitive -> HTuple（除非 kind 明确要求其它）
            if (value is int || value is uint || value is double || value is string || value is bool)
                return new HTuple(value);

            return value;
        }

        private void SetDictValue(HTuple dict, string key, HalconValueKind kind, object value)
        {
            // Case输入一般是JToken
            if (value is JToken jt)
            {
                if (jt.Type == JTokenType.Object)
                {
                    var jo = (JObject)jt;
                    var type = jo.Value<string>("type");

                    if (string.Equals(type, "image", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = jo.Value<string>("path");
                        var full = Path.IsPathRooted(path) ? path : Path.Combine(_baseDir, path);

                        HObject img;
                        HOperatorSet.ReadImage(out img, full);
                        HOperatorSet.SetDictObject(img, dict, key);
                        return;
                    }

                    if (string.Equals(type, "int", StringComparison.OrdinalIgnoreCase))
                    {
                        HOperatorSet.SetDictTuple(dict, key, new HTuple(jo.Value<int>("value")));
                        return;
                    }

                    if (string.Equals(type, "double", StringComparison.OrdinalIgnoreCase))
                    {
                        HOperatorSet.SetDictTuple(dict, key, new HTuple(jo.Value<double>("value")));
                        return;
                    }

                    if (string.Equals(type, "string", StringComparison.OrdinalIgnoreCase))
                    {
                        HOperatorSet.SetDictTuple(dict, key, new HTuple(jo.Value<string>("value") ?? string.Empty));
                        return;
                    }

                    if (string.Equals(type, "tuple", StringComparison.OrdinalIgnoreCase))
                    {
                        var arr = jo["value"] as JArray;
                        var t = new HTuple();
                        if (arr != null)
                        {
                            foreach (var x in arr)
                                t = t.TupleConcat((x.Type == JTokenType.Float) ? new HTuple(x.Value<double>()) : new HTuple(x.Value<int>()));
                        }
                        HOperatorSet.SetDictTuple(dict, key, t);
                        return;
                    }

                    throw new Exception($"Unsupported input object type '{type}' for key '{key}'");
                }

                // 简单值
                if (jt.Type == JTokenType.Integer) { HOperatorSet.SetDictTuple(dict, key, new HTuple(jt.Value<int>())); return; }
                if (jt.Type == JTokenType.Float) { HOperatorSet.SetDictTuple(dict, key, new HTuple(jt.Value<double>())); return; }
                if (jt.Type == JTokenType.String) { HOperatorSet.SetDictTuple(dict, key, new HTuple(jt.Value<string>())); return; }
            }

            // SharedContext/Constant 可能直接是 HTuple
            if (value is HTuple ht)
            {
                HOperatorSet.SetDictTuple(dict, key, ht);
                return;
            }

            throw new Exception($"Unsupported input value for key '{key}'");
        }

        public HTuple BuildRaw(Dictionary<string, JToken> caseInputs)
        {
            HTuple dict;
            HOperatorSet.CreateDict(out dict);

            if (caseInputs == null) return dict;

            foreach (var kv in caseInputs)
            {
                // 不知道 kind，就按 token 自己 type 推断（image/int/double/string/tuple）
                SetDictValue(dict, kv.Key, HalconValueKind.Any, kv.Value);
            }

            return dict;
        }

    }

}
