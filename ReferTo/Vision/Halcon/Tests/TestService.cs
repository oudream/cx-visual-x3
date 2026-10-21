using System;
using System.Collections.Generic;
using System.Linq;

namespace HalconDriverModule.Tests
{
    public sealed class SelfTestService
    {
        private readonly IHalconEngine _engine;
        private readonly ISelfTestOutput _out;
        private readonly string _casesDir;
        private readonly string _baseDir;
        private readonly Type _metaType;

        public SelfTestService(IHalconEngine engine, ISelfTestOutput output, string casesDir, string baseDir, Type metaType)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _out = output ?? throw new ArgumentNullException(nameof(output));
            _casesDir = casesDir ?? throw new ArgumentNullException(nameof(casesDir));
            _baseDir = baseDir ?? throw new ArgumentNullException(nameof(baseDir));
            _metaType = metaType ?? throw new ArgumentNullException(nameof(metaType));
        }

        /// <summary>
        /// 运行所有 ops 的所有 cases。
        /// runMode 语义：
        /// - Auto：严格按 opMeta.Invoke 执行；Invoke=Auto 时“优先 C#（若存在实现）否则 HDev”
        /// - CSharp：只跑能用 C# 跑的（HDev-only 跳过；Auto 但没 C# 实现也跳过）
        /// - HDev：只跑能用 HDev 跑的（CSharp-only 跳过；Auto 一律走 HDev）
        /// </summary>
        public SelfTestReport RunAll(HalconInvokeKind runMode = HalconInvokeKind.Auto)
        {
            var report = new SelfTestReport();

            var ops = HalconOpScanner.Scan(_metaType.Assembly);
            var opMap = ops.ToDictionary(x => x.Procedure, StringComparer.OrdinalIgnoreCase);
            var casesMap = CaseLoader.LoadAll(_casesDir);

            _out.Info($"[SelfTest] RunAll start. ops={ops.Count}, caseFiles={casesMap.Count}, runMode={runMode}");

            // 同一轮 RunAll 共享上下文（preStep/export 才能互通）
            var shared = new Dictionary<string, object>();
            var hdevRunner = new HalconSelfTestRunner(_engine, _baseDir, _out, shared);
            var csharpRunner = new CSharpSelfTestRunner(_baseDir, _out, shared);

            foreach (var op in ops)
            {
                if (!casesMap.TryGetValue(op.Procedure, out var cases) || cases == null || cases.Count == 0)
                {
                    _out.Warn($"[SelfTest] No cases for op={op.Procedure}");
                    continue;
                }

                foreach (var c in cases)
                {
                    // 1) preSteps：统一调度
                    if (!RunPreSteps(opMap, c, runMode, report, hdevRunner, csharpRunner))
                        continue;

                    // 2) main
                    var item = RunOneStrict(runMode, op, c, hdevRunner, csharpRunner);
                    report.Items.Add(item);

                    PrintItem(item);
                }
            }

            _out.Info($"[SelfTest] RunAll done. Pass={report.PassCount}, Fail={report.FailCount}");
            return report;
        }

        /// <summary>
        /// 单独运行某个 procedure 的所有 cases（含 preSteps）。
        /// </summary>
        public SelfTestReport RunProcedure(string procedure, HalconInvokeKind runMode = HalconInvokeKind.Auto)
        {
            if (string.IsNullOrWhiteSpace(procedure))
                throw new ArgumentNullException(nameof(procedure));

            var report = new SelfTestReport();

            var ops = HalconOpScanner.Scan(_metaType.Assembly);
            var opMap = ops.ToDictionary(x => x.Procedure, StringComparer.OrdinalIgnoreCase);

            if (!opMap.TryGetValue(procedure, out var opMeta))
            {
                _out.Error($"[SelfTest] RunProcedure: procedure '{procedure}' not found in scanned metadata.");
                report.Items.Add(new SelfTestItemReport
                {
                    Op = procedure,
                    CaseId = "N/A",
                    Success = false,
                    ElapsedMs = 0,
                    ErrorMessage = $"procedure '{procedure}' not found in scanned metadata"
                });
                return report;
            }

            var casesMap = CaseLoader.LoadAll(_casesDir);
            if (!casesMap.TryGetValue(procedure, out var cases) || cases == null || cases.Count == 0)
            {
                _out.Warn($"[SelfTest] RunProcedure: no cases for '{procedure}'");
                return report;
            }

            _out.Info($"[SelfTest] RunProcedure start. procedure={procedure}, cases={cases.Count}, runMode={runMode}");

            // 单次 RunProcedure 共享上下文
            var shared = new Dictionary<string, object>();
            var hdevRunner = new HalconSelfTestRunner(_engine, _baseDir, _out, shared);
            var csharpRunner = new CSharpSelfTestRunner(_baseDir, _out, shared);

            foreach (var c in cases)
            {
                if (!RunPreSteps(opMap, c, runMode, report, hdevRunner, csharpRunner))
                    continue;

                var item = RunOneStrict(runMode, opMeta, c, hdevRunner, csharpRunner);
                report.Items.Add(item);
                PrintItem(item);
            }

            _out.Info($"[SelfTest] RunProcedure done. procedure={procedure}, Pass={report.PassCount}, Fail={report.FailCount}");
            return report;
        }

        /// <summary>
        /// 压力测试某个 procedure。
        /// - iterations：重复次数（每次会跑该 procedure 的所有 cases）
        /// - stopOnFail：失败是否立刻停止
        ///
        /// 注意：每次 iteration 都会创建新的 sharedContext，避免跨轮污染。
        /// </summary>
        public SelfTestReport StressProcedure(string procedure, int iterations, HalconInvokeKind runMode = HalconInvokeKind.Auto, bool stopOnFail = true)
        {
            if (string.IsNullOrWhiteSpace(procedure))
                throw new ArgumentNullException(nameof(procedure));
            if (iterations <= 0)
                throw new ArgumentOutOfRangeException(nameof(iterations), "iterations must be > 0");

            var report = new SelfTestReport();

            var ops = HalconOpScanner.Scan(_metaType.Assembly);
            var opMap = ops.ToDictionary(x => x.Procedure, StringComparer.OrdinalIgnoreCase);

            if (!opMap.TryGetValue(procedure, out var opMeta))
            {
                _out.Error($"[SelfTest] StressProcedure: procedure '{procedure}' not found in scanned metadata.");
                report.Items.Add(new SelfTestItemReport
                {
                    Op = procedure,
                    CaseId = "N/A",
                    Success = false,
                    ElapsedMs = 0,
                    ErrorMessage = $"procedure '{procedure}' not found in scanned metadata"
                });
                return report;
            }

            var casesMap = CaseLoader.LoadAll(_casesDir);
            if (!casesMap.TryGetValue(procedure, out var cases) || cases == null || cases.Count == 0)
            {
                _out.Warn($"[SelfTest] StressProcedure: no cases for '{procedure}'");
                return report;
            }

            _out.Info($"[SelfTest] StressProcedure start. procedure={procedure}, cases={cases.Count}, iterations={iterations}, runMode={runMode}, stopOnFail={stopOnFail}");

            long totalMs = 0;
            long maxMs = 0;
            int totalItems = 0;
            int failItems = 0;

            for (int it = 1; it <= iterations; it++)
            {
                // 每轮独立 shared，避免污染
                var shared = new Dictionary<string, object>();
                var hdevRunner = new HalconSelfTestRunner(_engine, _baseDir, _out, shared);
                var csharpRunner = new CSharpSelfTestRunner(_baseDir, _out, shared);

                _out.Info($"[SelfTest] Stress iteration {it}/{iterations} begin: {procedure}");

                foreach (var c in cases)
                {
                    if (!RunPreSteps(opMap, c, runMode, report, hdevRunner, csharpRunner))
                    {
                        // preSteps 失败会记录 report Items
                        failItems++;
                        if (stopOnFail)
                        {
                            _out.Error($"[SelfTest] Stress stopped due to preSteps fail. iteration={it}, case={c.caseId}");
                            goto DONE;
                        }
                        continue;
                    }

                    var item = RunOneStrict(runMode, opMeta, c, hdevRunner, csharpRunner);
                    report.Items.Add(item);
                    PrintItem(item);

                    totalItems++;
                    totalMs += item.ElapsedMs;
                    if (item.ElapsedMs > maxMs) maxMs = item.ElapsedMs;

                    if (!item.Success)
                    {
                        failItems++;
                        if (stopOnFail)
                        {
                            _out.Error($"[SelfTest] Stress stopped due to FAIL. iteration={it}, case={c.caseId}");
                            goto DONE;
                        }
                    }
                }
            }

        DONE:
            var avg = totalItems > 0 ? (totalMs * 1.0 / totalItems) : 0;
            _out.Info($"[SelfTest] StressProcedure done. procedure={procedure}, totalItems={totalItems}, failItems={failItems}, avgMs={avg:F2}, maxMs={maxMs}");
            _out.Info($"[SelfTest] StressProcedure report: Pass={report.PassCount}, Fail={report.FailCount}");

            return report;
        }

        /// <summary>
        /// 统一跑 preSteps；失败则返回 false（并已写入 report）。
        /// </summary>
        private bool RunPreSteps(
            Dictionary<string, HalconOpMeta> opMap,
            CaseItem c,
            HalconInvokeKind runMode,
            SelfTestReport report,
            HalconSelfTestRunner hdevRunner,
            CSharpSelfTestRunner csharpRunner)
        {
            if (c.preSteps == null || c.preSteps.Count == 0)
                return true;

            foreach (var step in c.preSteps)
            {
                if (!opMap.TryGetValue(step.op, out var stepMeta))
                {
                    var miss = new SelfTestItemReport
                    {
                        Op = step.op,
                        CaseId = $"{c.caseId}::pre::{step.op}",
                        Success = false,
                        ElapsedMs = 0,
                        ErrorMessage = $"PreStep op '{step.op}' not found in scanned metadata"
                    };
                    report.Items.Add(miss);
                    PrintItem(miss);
                    return false;
                }

                var stepCase = new CaseItem
                {
                    caseId = $"{c.caseId}::pre::{step.op}",
                    inputs = step.inputs,
                    export = step.export,
                    expectOverrides = null,
                    perf = null,
                    preSteps = null
                };

                var stepRep = RunOneStrict(runMode, stepMeta, stepCase, hdevRunner, csharpRunner);
                report.Items.Add(stepRep);
                PrintItem(stepRep);

                if (!stepRep.Success)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 严格模式 + 运行过滤（runMode）。
        /// 规则：
        /// - runMode=CSharp：只跑能用 C# 的；不满足则 SKIP
        /// - runMode=HDev：只跑能用 HDev 的；不满足则 SKIP
        /// - runMode=Auto：
        ///     - Invoke=CSharp => 必须跑 C#
        ///     - Invoke=HDev   => 必须跑 HDev
        ///     - Invoke=Auto   => 优先 C#（若存在实现）否则 HDev
        /// </summary>
        private static SelfTestItemReport RunOneStrict(
            HalconInvokeKind runMode,
            HalconOpMeta opMeta,
            CaseItem c,
            HalconSelfTestRunner hdevRunner,
            CSharpSelfTestRunner csharpRunner)
        {
            bool hasCSharp = opMeta.Method != null;

            // 运行模式过滤（“直接过滤”）
            if (runMode == HalconInvokeKind.CSharp)
            {
                // 严格：HDev-only 直接跳过
                if (opMeta.Invoke == HalconInvokeKind.HDev)
                    return Skip(opMeta, c, "runMode=CSharp but op is HDev-only");

                // Auto 但没 C# 实现，也跳过
                if (opMeta.Invoke == HalconInvokeKind.Auto && !hasCSharp)
                    return Skip(opMeta, c, "runMode=CSharp but op has no C# implementation");

                // CSharp / (Auto+hasCSharp) -> 跑 C#
                return csharpRunner.RunSingle(opMeta, c);
            }

            if (runMode == HalconInvokeKind.HDev)
            {
                // 严格：CSharp-only 直接跳过
                if (opMeta.Invoke == HalconInvokeKind.CSharp)
                    return Skip(opMeta, c, "runMode=HDev but op is CSharp-only");

                // HDev / Auto -> 跑 HDev
                return hdevRunner.RunSingle(opMeta, c);
            }

            // runMode == Auto：严格模式 + Auto 优先 C#
            switch (opMeta.Invoke)
            {
                case HalconInvokeKind.CSharp:
                    return csharpRunner.RunSingle(opMeta, c);

                case HalconInvokeKind.HDev:
                    return hdevRunner.RunSingle(opMeta, c);

                case HalconInvokeKind.Auto:
                default:
                    return hasCSharp ? csharpRunner.RunSingle(opMeta, c)
                                     : hdevRunner.RunSingle(opMeta, c);
            }
        }

        private void PrintItem(SelfTestItemReport item)
        {
            if (item == null) return;

            // SKIP：Success=true 但有 [SKIP]
            if (item.Success && !string.IsNullOrEmpty(item.ErrorMessage) && item.ErrorMessage.StartsWith("[SKIP]"))
            {
                _out.Warn($"[SKIP] {item.Op}::{item.CaseId}  {item.ErrorMessage}");
                return;
            }

            if (item.Success)
                _out.Info($"[PASS] {item.Op}::{item.CaseId}  {item.ElapsedMs}ms");
            else
                _out.Error($"[FAIL] {item.Op}::{item.CaseId}  {item.ElapsedMs}ms  {item.ErrorMessage}");
        }

        private static SelfTestItemReport Skip(HalconOpMeta opMeta, CaseItem c, string reason)
        {
            return new SelfTestItemReport
            {
                Op = opMeta.Procedure,
                CaseId = c.caseId,
                Success = true,      // 过滤运行不算失败
                ElapsedMs = 0,
                ErrorMessage = "[SKIP] " + reason
            };
        }
    }

    public interface ISelfTestOutput
    {
        void Info(string msg);
        void Warn(string msg);
        void Error(string msg);
    }

    public sealed class SelfTestReport
    {
        public List<SelfTestItemReport> Items { get; } = new List<SelfTestItemReport>();
        public int PassCount => Items.Count(x => x.Success && (x.ErrorMessage == null || !x.ErrorMessage.StartsWith("[SKIP]")));
        public int FailCount => Items.Count(x => !x.Success);
        public int SkipCount => Items.Count(x => x.Success && x.ErrorMessage != null && x.ErrorMessage.StartsWith("[SKIP]"));
    }

    public sealed class SelfTestItemReport
    {
        public string Op { get; set; }
        public string CaseId { get; set; }
        public bool Success { get; set; }
        public int ElapsedMs { get; set; }
        public string ErrorMessage { get; set; }
    }

}
