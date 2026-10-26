using System;

namespace CxVisual.Mechanism.Data.Variables
{
    // 单变量乐观并发提交：ExpectedRevision在同一个状态锁中判断。
    public sealed class MDSVariableUpdateOptions
    {
        public MDSVariableUpdateOptions(string source = null,
            MDSVariableQuality quality = MDSVariableQuality.Good, long? expectedRevision = null)
        {
            if (!Enum.IsDefined(typeof(MDSVariableQuality), quality))
                throw new ArgumentOutOfRangeException("quality");
            if (expectedRevision < 0) throw new ArgumentOutOfRangeException("expectedRevision");
            Source = source; Quality = quality; ExpectedRevision = expectedRevision;
        }
        public string Source { get; private set; }
        public MDSVariableQuality Quality { get; private set; }
        public long? ExpectedRevision { get; private set; }
    }
    public sealed class MDSVariableUpdateResult
    {
        internal MDSVariableUpdateResult(string variableId, bool valueChanged, long revision,
            MDSVariableQuality quality, DateTime refreshUtc)
        {
            VariableId = variableId; ValueChanged = valueChanged; Revision = revision;
            Quality = quality; RefreshUtc = refreshUtc;
        }
        public string VariableId { get; private set; }
        public bool ValueChanged { get; private set; }
        public long Revision { get; private set; }
        public MDSVariableQuality Quality { get; private set; }
        public DateTime RefreshUtc { get; private set; }
    }
    public sealed class MDSVariableVersionConflictException : InvalidOperationException
    {
        public MDSVariableVersionConflictException(string id, long expected, long actual)
            : base("变量提交版本冲突: " + id + " expected=" + expected + " actual=" + actual) { }
    }
}
