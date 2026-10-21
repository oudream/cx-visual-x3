using System;
using System.Collections.Generic;

namespace HalconDriverModule.Tests
{
    /// <summary>
    /// 仅负责：构造 InDict -> 执行 HDev 过程 -> Validate -> Perf。
    /// 不负责：preSteps 调度 / CSharp-HDev 选择策略。
    /// </summary>
    public sealed class HalconSelfTestRunner
    {
        private readonly IHalconEngine _engine;
        private readonly InDictBuilder _builder;
        private readonly ISelfTestOutput _out;
        private readonly Dictionary<string, object> _shared;

        public HalconSelfTestRunner(IHalconEngine engine, string baseDir, ISelfTestOutput output)
            : this(engine, baseDir, output, new Dictionary<string, object>())
        {
        }

        public HalconSelfTestRunner(IHalconEngine engine, string baseDir, ISelfTestOutput output, Dictionary<string, object> shared)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
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
                // 1) build input
                var inDict = _builder.Build(opMeta.Inputs, c.inputs, _shared);

                // 2) execute
                _engine.ExecuteProcedure(opMeta.Procedure, inDict, out var outDict, out var errMsg);

                // 3) err check
                if (!string.IsNullOrEmpty(errMsg))
                    throw new Exception($"ErrMsg: {errMsg}");

                // 4) export to shared（可选）
                if (c.export != null)
                {
                    foreach (var kv in c.export)
                    {
                        if (!OutDictReader.TryGetTuple(outDict, kv.Key, out var t))
                            throw new Exception($"Missing export '{kv.Key}'");

                        _shared[kv.Value] = t;
                    }
                }

                // 5) validate
                Validators.Validate(opMeta.Outputs, c.expectOverrides, outDict);

                // 6) perf
                if (c.perf?.maxMs != null && sw.ElapsedMilliseconds > c.perf.maxMs.Value)
                    throw new Exception($"Perf exceeded: {sw.ElapsedMilliseconds}ms > {c.perf.maxMs.Value}ms");

                rep.Success = true;
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
    }
}
