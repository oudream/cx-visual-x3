using System;
using System.IO;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ConfigArchitectureCompletionTests
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxVisual-Config-Atomic-" + Guid.NewGuid().ToString("N"));
            try { VerifyAtomicGroup(root); }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            Console.WriteLine("PASS atomic multi-model configuration transaction, validation and snapshots");
        }

        private static void VerifyAtomicGroup(string root)
        {
            MHSConfigService service = new MHSConfigService(root);
            MHSConfigAtomicGroup group = service.OpenAtomicGroup("Inspection.Setup");
            group.Register<V13ConfigSample>("camera");
            group.Register<V13AsyncConfig>("algorithm");
            group.Seal();
            bool allowed = false;
            group.ConfigureApplyGuard(review => allowed
                ? MHSConfigApplyDecision.Allow()
                : MHSConfigApplyDecision.Deny("HOLD", "尚未授权整组生效"));
            string before = group.SavedVersion;
            using (MHSConfigAtomicEdit edit = group.BeginEdit("成组相机与算法设置"))
            {
                V13ConfigSample camera = edit.Get<V13ConfigSample>("camera");
                camera.ExposureMs = 155;
                V13AsyncConfig algorithm = edit.Get<V13AsyncConfig>("algorithm");
                algorithm.Mode = 1; // Async rule explicitly refuses this member.
                edit.Set("camera", camera);
                edit.Set("algorithm", algorithm);
                MHSConfigCommitResult rejected = edit.CommitAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(!rejected.Success && rejected.ErrorCode == "CONFIG_GROUP_VALIDATION" &&
                    group.SavedVersion == before, "one invalid member rolls back entire group");
                algorithm.Mode = 0;
                edit.Set("algorithm", algorithm);
                MHSConfigCommitResult committed = edit.CommitAsync(CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(committed.Success && committed.PendingApply &&
                    group.SavedVersion != before && group.EffectiveVersion == before,
                    "two distinct models commit under one revision and remain pending");
            }
            using (MHSConfigGroupSnapshot applied = group.CaptureForTask("job-previous"))
            using (MHSConfigGroupSnapshot staged = group.CaptureSaved())
            {
                Check(applied.Get<V13ConfigSample>("camera").ExposureMs == 80 &&
                    applied.Get<V13AsyncConfig>("algorithm").Count == 2 &&
                    staged.Get<V13ConfigSample>("camera").ExposureMs == 155,
                    "effective group remains old while saved group is new");
            }
            MHSConfigApplyResult denial = group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null);
            Check(!denial.Applied && denial.Status == "HOLD" &&
                group.EffectiveVersion == before, "mandatory guard holds entire group");
            allowed = true;
            MHSConfigApplyResult activated = group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null);
            Check(activated.Applied && group.EffectiveVersion == group.SavedVersion,
                "entire group activates with one durable marker");
            using (MHSConfigGroupSnapshot fixedTask = group.CaptureForTask("job-001"))
            {
                Check(fixedTask.MemberKeys.SequenceEqual(new[] { "algorithm", "camera" }) &&
                    fixedTask.Get<V13ConfigSample>("camera").ExposureMs == 155 &&
                    fixedTask.Version == group.EffectiveVersion,
                    "strongly typed two-model task snapshot and one version");
                MHSConfigAtomicEdit first = group.BeginEdit("编辑1");
                MHSConfigAtomicEdit stale = group.BeginEdit("并发过期编辑2");
                try
                {
                    V13ConfigSample camera = first.Get<V13ConfigSample>("camera");
                    camera.ExposureMs = 190;
                    first.Set("camera", camera);
                    Check(first.CommitAsync(CancellationToken.None).GetAwaiter().GetResult().Success,
                        "first group writer committed");
                    V13AsyncConfig algorithm = stale.Get<V13AsyncConfig>("algorithm");
                    algorithm.Count = 11;
                    stale.Set("algorithm", algorithm);
                    MHSConfigCommitResult conflict = stale.CommitAsync(CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(!conflict.Success && conflict.ErrorCode == "CONFIG_REVISION_CONFLICT",
                        "entire second concurrent edit rejected, no partial overwrite");
                }
                finally { first.Dispose(); stale.Dispose(); }
                Check(group.EffectiveVersion == fixedTask.Version &&
                    fixedTask.Get<V13ConfigSample>("camera").ExposureMs == 155 &&
                    fixedTask.Get<V13AsyncConfig>("algorithm").Count == 2,
                    "pinned task retains both old values after later commit");
            }
            MHSConfigService restarted = new MHSConfigService(root);
            MHSConfigAtomicGroup loaded = restarted.OpenAtomicGroup("Inspection.Setup");
            loaded.Register<V13ConfigSample>("camera");
            loaded.Register<V13AsyncConfig>("algorithm");
            loaded.Seal();
            using (MHSConfigGroupSnapshot active = loaded.CaptureForTask("reopened"))
            using (MHSConfigGroupSnapshot staged = loaded.CaptureSaved())
                Check(active.Get<V13ConfigSample>("camera").ExposureMs == 155 &&
                    staged.Get<V13ConfigSample>("camera").ExposureMs == 190 &&
                    loaded.HasPendingApply, "restart rehydrates saved and effective atomically");
            Check(loaded.GetStatus().AuditCount == 2,
                "two committed multi-model transactions have one audit entry each");
            Expect<InvalidOperationException>(() => loaded.Register<V13ConfigSample>("extra"),
                "sealed registry refuses silent schema change");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("CONFIG_COMPLETION: " + message); }
        private static void Expect<T>(Action action, string message) where T : Exception
        { try { action(); } catch (T) { return; } throw new InvalidOperationException("CONFIG_COMPLETION: " + message); }
    }
}
