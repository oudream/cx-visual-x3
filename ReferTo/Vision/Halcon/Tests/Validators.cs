using HalconDotNet;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule.Tests
{
    // 读取输出并断言
    public static class Validators
    {
        public static void Validate(
            HalconOutAttribute[] outputs,
            Dictionary<string, JObject> overrides,
            HTuple outDict)
        {
            foreach (var o in outputs)
            {
                var ov = (overrides != null && overrides.TryGetValue(o.Key, out var tmp)) ? tmp : null;

                bool required = ov?.Value<bool?>("required") ?? o.Required;

                // kind 分两类：tuple / object
                if (IsObjectKind(o.Kind))
                {
                    if (!OutDictReader.TryGetObject(outDict, o.Key, out var obj))
                    {
                        if (required) throw new Exception($"Missing output object '{o.Key}'");
                        continue;
                    }

                    // 基础有效性检查：对象必须初始化
                    if (!obj.IsInitialized())
                        throw new Exception($"Output object '{o.Key}' is not initialized");
                }
                else
                {
                    if (!OutDictReader.TryGetTuple(outDict, o.Key, out var t))
                    {
                        if (required) 
                            throw new Exception($"Missing output tuple '{o.Key}'");
                        continue;
                    }

                    // 长度
                    int length = (ov?.Value<int?>("length")) ?? o.Length;
                    if (length >= 0 && t.Length != length)
                        throw new Exception($"Output '{o.Key}' length expected {length} but got {t.Length}");

                    // oneOf（对 tuple 的首元素做判断）
                    var oneOf = ov?["oneOf"]?.ToObject<string[]>() ?? o.OneOf;
                    if (oneOf != null && oneOf.Length > 0)
                    {
                        var v = (t.Length > 0) ? t[0].S : "";
                        if (!oneOf.Contains(v))
                            throw new Exception($"Output '{o.Key}' expected oneOf [{string.Join(",", oneOf)}] but got '{v}'");
                    }

                    // range（对 tuple 的首元素做数值判断）
                    double min = (ov?.Value<double?>("min")) ?? o.RangeMin;
                    double max = (ov?.Value<double?>("max")) ?? o.RangeMax;

                    bool hasMin = !double.IsNaN(min);
                    bool hasMax = !double.IsNaN(max);

                    if (hasMin || hasMax)
                    {
                        double dv;
                        try { dv = t[0].D; } catch { dv = t[0].I; }

                        if (hasMin && dv < min) throw new Exception($"Output '{o.Key}' expected >= {min} but got {dv}");
                        if (hasMax && dv > max) throw new Exception($"Output '{o.Key}' expected <= {max} but got {dv}");
                    }
                }
            }
        }

        private static bool IsObjectKind(HalconValueKind k)
        {
            return k == HalconValueKind.Image || k == HalconValueKind.Region || k == HalconValueKind.Xld;
        }
    }
}
