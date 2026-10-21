using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HalconDriverModule.Tests
{
    /// <summary>
    /// 仅负责：构造 C# invoke 参数 -> 反射调用 -> BuildOutDict -> Validate -> Perf。
    /// 不负责：preSteps 调度 / CSharp-HDev 选择策略。
    /// </summary>
    public sealed class CSharpSelfTestRunner
    {
        private readonly InDictBuilder _builder;
        private readonly ISelfTestOutput _out;
        private readonly Dictionary<string, object> _shared;

        public CSharpSelfTestRunner(string baseDir, ISelfTestOutput output)
            : this(baseDir, output, new Dictionary<string, object>())
        {
        }

        public CSharpSelfTestRunner(string baseDir, ISelfTestOutput output, Dictionary<string, object> shared)
        {
            _builder = new InDictBuilder(baseDir);
            _out = output;
            _shared = shared ?? new Dictionary<string, object>();
        }

        public SelfTestItemReport RunSingle(HalconOpMeta opMeta, CaseItem c)
        {
            if (opMeta == null) throw new ArgumentNullException(nameof(opMeta));
            if (c == null) throw new ArgumentNullException(nameof(c));

            var rep = new SelfTestItemReport
            {
                Op = opMeta.Procedure,
                CaseId = c.caseId
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var method = opMeta.Method;
                if (method == null)
                    throw new Exception("opMeta.Method is null (no C# implementation)");

                if (!method.IsStatic || !method.IsPublic)
                    throw new Exception($"C# runner only supports public static method: {method.DeclaringType?.FullName}.{method.Name}");

                // 1) build args
                var args = BuildInvokeArgs(opMeta, c.inputs);

                // 2) invoke
                var ret = method.Invoke(null, args);

                // 3) 使用新的 ExtractResult 方法
                var (errMsg, result) = ExtractResult(ret, method);
                if (!string.IsNullOrEmpty(errMsg))
                    throw new Exception($"ErrMsg: {errMsg}");

                // 4) build outDict - 现在有了 result 参数
                var outDict = BuildOutDict(opMeta, method, args, ret, result);

                // 5) export to shared（可选）
                //if (c.export != null)
                //{
                //    foreach (var kv in c.export)
                //    {
                //        if (!OutDictReader.TryGetTuple(outDict, kv.Key, out var t))
                //            throw new Exception($"Missing export '{kv.Key}'");

                //        _shared[kv.Value] = t;
                //    }
                //}

                if (c.export != null && c.export.Count > 0)
                {
                    foreach (var kv in c.export)
                    {
                        string exportKey = kv.Key;
                        string sharedKey = kv.Value?.ToString() ?? exportKey;

                        // 先尝试作为对象获取（图像、区域等）
                        if (OutDictReader.TryGetObject(outDict, exportKey, out var obj))
                        {
                            _shared[sharedKey] = obj;
                        }
                        // 再尝试作为元组获取（数值、字符串等）
                        else if (OutDictReader.TryGetTuple(outDict, exportKey, out var tuple))
                        {
                            _shared[sharedKey] = tuple;
                        }
                        // 都没找到，抛出异常
                        else
                        {
                            throw new Exception($"Missing export '{exportKey}'");
                        }
                    }
                }

                // 6) validate
                Validators.Validate(opMeta.Outputs, c.expectOverrides, outDict);

                // 7) perf
                if (c.perf?.maxMs != null && sw.ElapsedMilliseconds > c.perf.maxMs.Value)
                    throw new Exception($"Perf exceeded: {sw.ElapsedMilliseconds}ms > {c.perf.maxMs.Value}ms");

                rep.Success = true;
            }
            catch (TargetInvocationException tie)
            {
                rep.Success = false;
                rep.ErrorMessage = tie.InnerException?.Message ?? tie.Message;
            }
            catch (Exception ex)
            {
                rep.Success = false;
                rep.ErrorMessage = ex.Message;
            }
            finally
            {
                sw.Stop();
                rep.ElapsedMs = (int)sw.ElapsedMilliseconds;
            }

            return rep;
        }

        private object[] BuildInvokeArgs(HalconOpMeta opMeta, Dictionary<string, Newtonsoft.Json.Linq.JToken> caseInputs)
        {
            var method = opMeta.Method;
            var ps = method.GetParameters();
            var args = new object[ps.Length];

            // 1) 先把 caseInputs/Shared/Constant 解析成“候选值”字典（按 key）
            var resolved = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (opMeta.Inputs != null)
            {
                foreach (var input in opMeta.Inputs)
                {
                    object raw;
                    switch (input.Source)
                    {
                        case HalconValueSource.CaseInput:
                            if (caseInputs == null || !caseInputs.TryGetValue(input.Key, out var jt))
                            {
                                if (input.Required) throw new Exception($"Missing required input '{input.Key}'");
                                continue;
                            }
                            raw = _builder.ResolveRawValue(input.Kind, jt);
                            break;

                        case HalconValueSource.SharedContext:
                            if (string.IsNullOrEmpty(input.ContextKey))
                                throw new Exception($"Input '{input.Key}' Source=SharedContext but ContextKey is empty");
                            if (!_shared.TryGetValue(input.ContextKey, out raw))
                            {
                                if (input.Required) throw new Exception($"Missing shared '{input.ContextKey}' for '{input.Key}'");
                                continue;
                            }
                            raw = _builder.ResolveRawValue(input.Kind, raw);
                            break;

                        case HalconValueSource.Constant:
                            raw = _builder.ResolveRawValue(input.Kind, input.Constant);
                            break;

                        default:
                            raw = null;
                            break;
                    }

                    resolved[input.Key] = raw;

                    // 如果 C# 方法没有显式参数，但需要注入到引擎类（例如 ConfigDict），尝试调用 Set{Key}(...) 或 写静态属性
                    if (!HasParam(ps, input.Param ?? input.Key))
                        TryInjectToDeclaringType(method.DeclaringType, input.Key, raw);
                }
            }

            // 2) 给每个参数赋值
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                if (p.IsOut)
                {
                    args[i] = GetDefault(p.ParameterType.GetElementType());
                    continue;
                }

                // 根据 Param 映射找 key
                var key = FindKeyForParam(opMeta, p.Name);
                if (key == null)
                {
                    if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }
                    args[i] = GetDefault(p.ParameterType);
                    continue;
                }

                if (!resolved.TryGetValue(key, out var val))
                {
                    if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }
                    args[i] = GetDefault(p.ParameterType);
                    continue;
                }

                args[i] = ConvertToParamType(val, p.ParameterType);
            }

            return args;
        }

        private static bool HasParam(ParameterInfo[] ps, string paramName)
            => ps.Any(p => string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

        private static string FindKeyForParam(HalconOpMeta opMeta, string paramName)
        {
            if (opMeta.Inputs == null) return null;
            foreach (var a in opMeta.Inputs)
            {
                var pn = string.IsNullOrEmpty(a.Param) ? a.Key : a.Param;
                if (string.Equals(pn, paramName, StringComparison.OrdinalIgnoreCase))
                    return a.Key;
            }
            return null;
        }

        private static void TryInjectToDeclaringType(Type declaringType, string key, object value)
        {
            if (declaringType == null) return;

            // 方法：Set{Key}(T)
            var m = declaringType.GetMethod("Set" + key, BindingFlags.Public | BindingFlags.Static);
            if (m != null && m.GetParameters().Length == 1)
            {
                var pt = m.GetParameters()[0].ParameterType;
                m.Invoke(null, new[] { ConvertSimple(value, pt) });
                return;
            }

            // 属性：{Key}
            var p = declaringType.GetProperty(key, BindingFlags.Public | BindingFlags.Static);
            if (p != null && p.CanWrite)
            {
                p.SetValue(null, ConvertSimple(value, p.PropertyType));
            }
        }

        private static object ConvertSimple(object v, Type targetType)
        {
            if (v == null) return null;
            if (targetType.IsInstanceOfType(v)) return v;

            if (targetType == typeof(HTuple))
            {
                if (v is HTuple ht) return ht;
                return new HTuple(v);
            }

            if (targetType == typeof(uint)) return Convert.ToUInt32(UnwrapHTuple(v));
            if (targetType == typeof(int)) return Convert.ToInt32(UnwrapHTuple(v));
            if (targetType == typeof(double)) return Convert.ToDouble(UnwrapHTuple(v));
            if (targetType == typeof(string)) return Convert.ToString(UnwrapHTuple(v));

            return Convert.ChangeType(UnwrapHTuple(v), targetType);
        }

        private static object ConvertToParamType(object v, Type paramType)
        {
            if (v == null) return null;

            // ref/out 的元素类型
            if (paramType.IsByRef) paramType = paramType.GetElementType();

            // 如果类型已经匹配，直接返回
            if (paramType.IsInstanceOfType(v)) return v;

            // ========== 首先处理 List<HObject> 类型 ==========
            if (paramType == typeof(List<HObject>))
            {
                // 情况1：已经是 List<HObject>
                if (v is List<HObject> imageList)
                {
                    return imageList;
                }
                // 情况2：单个 HObject，包装成列表
                else if (v is HObject singleImage)
                {
                    return new List<HObject> { singleImage };
                }
                //// 情况3：HTuple 包含 HObject（理论上不应该出现）
                //else if (v is HTuple ht)
                //{
                //    List<HObject> list = new List<HObject>();
                //    for (int i = 0; i < ht.Length; i++)
                //    {
                //        object element = ht[i];
                //        if (element is HObject ho)
                //        {
                //            list.Add(ho);
                //        }
                //    }
                //    return list;
                //}
            }

            // ========== 处理数组类型 ==========
            if (paramType.IsArray && v is HTuple ht2)
            {
                Type elementType = paramType.GetElementType();

                if (elementType == typeof(int))
                {
                    int[] array = new int[ht2.Length];
                    for (int i = 0; i < ht2.Length; i++)
                    {
                        array[i] = ht2[i].I;
                    }
                    return array;
                }
                // 可以添加其他数组类型的处理
            }

            // HTuple → 基础类型（单值）
            if (v is HTuple ht3)
            {
                var u = UnwrapHTuple(ht3);
                if (paramType == typeof(uint)) return Convert.ToUInt32(u);
                if (paramType == typeof(int)) return Convert.ToInt32(u);
                if (paramType == typeof(double)) return Convert.ToDouble(u);
                if (paramType == typeof(string)) return Convert.ToString(u);
            }

            // 处理 HObject 类型
            if (paramType == typeof(HObject) && v is HObject ho)
            {
                return ho;
            }

            if (paramType == typeof(HTuple))
                return (v is HTuple ht4) ? ht4 : new HTuple(v);

            // 最后尝试通用转换
            return Convert.ChangeType(UnwrapHTuple(v), paramType);
        }

        private static object UnwrapHTuple(object v)
        {
            if (v is HTuple ht)
            {
                if (ht.Length <= 0) return null;
                if (ht.Type == HTupleType.INTEGER) return ht.I;
                if (ht.Type == HTupleType.DOUBLE) return ht.D;
                return ht.S;
            }
            return v;
        }

        private static object GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;

        private static (string errMsg, object result) ExtractResult(object ret, MethodInfo method)
        {
            if (ret == null) return (null, null);

            var methodName = method.Name.ToLower();

            // 1. 如果是字符串
            if (ret is string s)
            {
                if (methodName.Contains("version"))
                {
                    // GetVersion: 返回版本号，空字符串表示失败
                    if (string.IsNullOrEmpty(s))
                    {
                        return ("GetVersion returned empty string", null);
                    }
                    else
                    {
                        return (null, s);
                    }
                }

                if (string.IsNullOrEmpty(s))
                {
                    return (null, s);
                }
                else
                {
                    return (s, null);
                }
            }

            // 2. 布尔值
            if (ret is bool boolVal)
            {
                if (boolVal)
                {
                    return (null, boolVal);
                }
                else
                {
                    return ("Function returned false", null);
                }
            }

            // 3. ValueTuple<string, string>
            var rt = ret.GetType();
            if (rt.IsValueType && rt.FullName.StartsWith("System.ValueTuple"))
            {
                var f1 = rt.GetField("Item1");
                var f2 = rt.GetField("Item2");

                if (f1 != null && f1.FieldType == typeof(string))
                {
                    var errMsg = (string)f1.GetValue(ret);
                    object info = null;

                    if (f2 != null && f2.FieldType == typeof(string))
                    {
                        info = f2.GetValue(ret);
                    }

                    return string.IsNullOrEmpty(errMsg) ? (null, info) : (errMsg, info);
                }
            }

            // 4. Tuple<string, string>
            if (ret is Tuple<string, string> tuple)
            {
                return string.IsNullOrEmpty(tuple.Item1) ? (null, tuple.Item2) : (tuple.Item1, tuple.Item2);
            }

            // 5. 数值类型
            if (ret is int || ret is uint || ret is double || ret is float || ret is long)
            {
                return (null, ret);
            }

            // 6. 其他类型
            var str = ret.ToString();
            if (!str.StartsWith(ret.GetType().FullName))
            {
                return (str, null);
            }

            return (null, ret);
        }

        private static string ExtractInfo(object ret)
        {
            if (ret == null) return null;
            var rt = ret.GetType();
            if (rt.IsValueType && rt.FullName.StartsWith("System.ValueTuple"))
            {
                var f2 = rt.GetField("Item2");
                if (f2 != null && f2.FieldType == typeof(string))
                    return (string)f2.GetValue(ret);
            }
            if (ret is Tuple<string, string> tup) return tup.Item2;
            return null;
        }

        // 修改 BuildOutDict，增加 result 参数
        private static HTuple BuildOutDict(HalconOpMeta opMeta, MethodInfo method, object[] args, object ret, object result = null)
        {
            HOperatorSet.CreateDict(out var outDict);

            // 首先，如果有返回值结果，尝试设置到字典
            if (result != null && opMeta.Outputs != null)
            {
                // 查找是否有对应的输出注解
                foreach (var output in opMeta.Outputs)
                {
                    // 简单的匹配规则
                    var methodName = method.Name.ToLower();

                    if (methodName.Contains("version") && output.Key.Equals("Version", StringComparison.OrdinalIgnoreCase))
                    {
                        SetDictByKind(outDict, output.Key, output.Kind, result);
                        break;
                    }

                    if (methodName.Contains("ratio") && output.Key.Equals("Ratio", StringComparison.OrdinalIgnoreCase))
                    {
                        SetDictByKind(outDict, output.Key, output.Kind, result);
                        break;
                    }

                    // 通用规则：如果只有一个输出，使用它
                    if (opMeta.Outputs.Length == 1)
                    {
                        SetDictByKind(outDict, output.Key, output.Kind, result);
                        break;
                    }
                }
            }

            // out/ref 参数 & 静态导出
            if (opMeta.Outputs != null)
            {
                foreach (var o in opMeta.Outputs)
                {
                    //// 跳过已经设置过的
                    //if (IsKeyInDict(outDict, o.Key))
                    //    continue;

                    // ========== 新增：处理返回值 IsOK ==========
                    if (string.Equals(o.Key, "IsOK", StringComparison.OrdinalIgnoreCase))
                    {
                        // IsOK 通常是方法的返回值
                        // 注意：ret 可能是 bool 类型，需要转换为 HTuple
                        object isOkValue = ret;
                        if (ret is bool boolValue)
                        {
                            // 转换 bool 为 int (true -> 1, false -> 0)
                            isOkValue = boolValue ? 1 : 0;
                        }
                        SetDictByKind(outDict, o.Key, o.Kind, isOkValue);
                        continue;
                    }

                    // 特例：Info 来自返回值 Item2
                    if (string.Equals(o.Key, "Info", StringComparison.OrdinalIgnoreCase))
                    {
                        var info = ExtractInfo(ret);
                        if (!string.IsNullOrEmpty(info))
                            HOperatorSet.SetDictTuple(outDict, o.Key, new HTuple(info));
                        continue;
                    }

                    // 静态导出
                    if (!string.IsNullOrEmpty(o.ExportFromStaticProperty) || !string.IsNullOrEmpty(o.ExportFromStaticMethod))
                    {
                        var val = ExportStatic(method.DeclaringType, o);
                        if (val != null)
                            SetDictByKind(outDict, o.Key, o.Kind, val);
                        continue;
                    }

                    var paramName = string.IsNullOrEmpty(o.Param) ? o.Key : o.Param;
                    var idx = FindParamIndex(method, paramName);
                    if (idx < 0) continue;

                    var p = method.GetParameters()[idx];
                    if (!p.IsOut && !p.ParameterType.IsByRef) continue;

                    var val2 = args[idx];
                    if (val2 == null) continue;

                    SetDictByKind(outDict, o.Key, o.Kind, val2);
                }
            }

            return outDict;
        }

        // 检查键是否已经在字典中
        private static bool IsKeyInDict(HTuple dict, string key)
        {
            try
            {
                HTuple value;
                HOperatorSet.GetDictTuple(dict, key, out value);
                return value != null && value.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static object ExportStatic(Type declaringType, HalconOutAttribute o)
        {
            if (declaringType == null) return null;

            if (!string.IsNullOrEmpty(o.ExportFromStaticProperty))
            {
                var p = declaringType.GetProperty(o.ExportFromStaticProperty, BindingFlags.Public | BindingFlags.Static);
                if (p != null) return p.GetValue(null);
            }

            if (!string.IsNullOrEmpty(o.ExportFromStaticMethod))
            {
                var m = declaringType.GetMethod(o.ExportFromStaticMethod, BindingFlags.Public | BindingFlags.Static);
                if (m != null && m.GetParameters().Length == 0) return m.Invoke(null, null);
            }

            return null;
        }

        private static int FindParamIndex(MethodInfo method, string paramName)
        {
            var ps = method.GetParameters();

            // 1. 先按参数名查找（原逻辑）
            for (int i = 0; i < ps.Length; i++)
            {
                if (string.Equals(ps[i].Name, paramName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            // 2. 检查 HalconIn 特性
            for (int i = 0; i < ps.Length; i++)
            {
                var halconInAttr = ps[i].GetCustomAttribute<HalconInAttribute>();
                if (halconInAttr != null)
                {
                    // 检查 Key 或 Param 属性
                    if (!string.IsNullOrEmpty(halconInAttr.Key) &&
                        string.Equals(halconInAttr.Key, paramName, StringComparison.OrdinalIgnoreCase))
                        return i;

                    if (!string.IsNullOrEmpty(halconInAttr.Param) &&
                        string.Equals(halconInAttr.Param, paramName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            // 3. 检查 HalconOut 特性 - 修复这里！
            for (int i = 0; i < ps.Length; i++)
            {
                var halconOutAttr = ps[i].GetCustomAttribute<HalconOutAttribute>();
                if (halconOutAttr != null)
                {
                    // 检查 Key
                    if (!string.IsNullOrEmpty(halconOutAttr.Key) &&
                        string.Equals(halconOutAttr.Key, paramName, StringComparison.OrdinalIgnoreCase))
                        return i;

                    // 添加：检查 Param（这是缺失的部分！）
                    if (!string.IsNullOrEmpty(halconOutAttr.Param) &&
                        string.Equals(halconOutAttr.Param, paramName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return -1;
        }

        private static void SetDictByKind(HTuple dict, string key, HalconValueKind kind, object val)
        {
            if (val is HObject ho)
            {
                HOperatorSet.SetDictObject(ho, dict, key);
                return;
            }

            if (val is HTuple ht)
            {
                HOperatorSet.SetDictTuple(dict, key, ht);
                return;
            }

            HOperatorSet.SetDictTuple(dict, key, new HTuple(val));
        }
    }
}
