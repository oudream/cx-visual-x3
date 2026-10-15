using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// 失败流程显式补偿报告；补偿结果与原流程结果分开保存，失败不会丢失。
    /// </summary>
    public sealed class MRKCompensationReport
    {
        internal MRKCompensationReport(IList<string> succeeded,
            IDictionary<string, Exception> failed)
        {
            SucceededSteps = new ReadOnlyCollection<string>(new List<string>(succeeded));
            Failures = new ReadOnlyDictionary<string, Exception>(
                new Dictionary<string, Exception>(failed, StringComparer.Ordinal));
        }
        public IList<string> SucceededSteps { get; private set; }
        public IDictionary<string, Exception> Failures { get; private set; }
        public bool Succeeded { get { return Failures.Count == 0; } }
    }
}
