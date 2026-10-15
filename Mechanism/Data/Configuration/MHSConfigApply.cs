using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>生效前审查使用的隔离快照，不允许审查方修改配置真源。</summary>
    public sealed class MHSConfigApplyReview<T>
    {
        internal MHSConfigApplyReview(T saved, T effective, string savedVersion, string effectiveVersion,
            FCMConfigApplyPolicy required, IList<string> paths)
        {
            Candidate = saved;
            Current = effective;
            SavedVersion = savedVersion; EffectiveVersion = effectiveVersion;
            RequiredBoundary = required;
            ChangedPaths = new List<string>(paths).AsReadOnly();
        }
        public T Candidate { get; private set; }
        public T Current { get; private set; }
        public string SavedVersion { get; private set; }
        public string EffectiveVersion { get; private set; }
        public FCMConfigApplyPolicy RequiredBoundary { get; private set; }
        public IList<string> ChangedPaths { get; private set; }
    }

    /// <summary>可由权限、联锁、设备状态适配器返回；基础体系不依赖硬件 SDK。</summary>
    public sealed class MHSConfigApplyDecision
    {
        private MHSConfigApplyDecision(bool approved, string code, string reason)
        { Approved = approved; Code = code; Reason = reason; }
        public bool Approved { get; private set; }
        public string Code { get; private set; }
        public string Reason { get; private set; }
        public static MHSConfigApplyDecision Allow()
        { return new MHSConfigApplyDecision(true, "APPROVED", null); }
        public static MHSConfigApplyDecision Deny(string code, string reason)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Denial code required", "code");
            return new MHSConfigApplyDecision(false, code, reason);
        }
    }
    public sealed class MHSConfigApplyResult
    {
        public bool Applied { get; internal set; }
        public string Status { get; internal set; }
        public string Reason { get; internal set; }
        public string EffectiveVersion { get; internal set; }
        public string SavedVersion { get; internal set; }
        public FCMConfigApplyPolicy RequiredBoundary { get; internal set; }
    }
}
