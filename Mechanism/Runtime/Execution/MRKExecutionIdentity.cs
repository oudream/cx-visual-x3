using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// 每次Flow固定的诊断关联；不可变身份供代码、编排、事件和将来的Studio观察共用。
    /// 不是用户数据栈，不携带密码或设备控制权。
    /// </summary>
    public sealed class MRKFlowMetadata
    {
        public MRKFlowMetadata(long planRevision = 0, string businessKey = null,
            string correlationId = null, string sessionId = null, Guid? parentFlowId = null)
        {
            if (planRevision < 0) throw new ArgumentOutOfRangeException("planRevision");
            PlanRevision = planRevision; BusinessKey = businessKey;
            CorrelationId = correlationId; SessionId = sessionId;
            ParentFlowId = parentFlowId;
        }
        public long PlanRevision { get; private set; }
        public string BusinessKey { get; private set; }
        public string CorrelationId { get; private set; }
        public string SessionId { get; private set; }
        public Guid? ParentFlowId { get; private set; }
    }

    /// <summary>对工程师显示的运行节点事实快照，独立于具体线程。</summary>
    public sealed class MRKStepObservation
    {
        internal MRKStepObservation(string id, MRKStepState state, Guid? workId,
            string reason, DateTime changedUtc)
        { StepId = id; State = state; WorkId = workId;
          Reason = reason; ChangedUtc = changedUtc; }
        public string StepId { get; private set; }
        public MRKStepState State { get; private set; }
        public Guid? WorkId { get; private set; }
        public string Reason { get; private set; }
        public DateTime ChangedUtc { get; private set; }
    }
    /// <summary>一次Flow的进展视图：取消请求与终态分离；汇总状态不等于设备安全确认。</summary>
    public sealed class MRKFlowSnapshot
    {
        internal MRKFlowSnapshot(Guid id, string planId, MRKFlowMetadata metadata,
            MRKFlowState state, bool cancelRequested, DateTime createdUtc,
            DateTime? endedUtc, int total, int completed, int waiting,
            int failed, int skipped,
            IDictionary<string,MRKStepState> regionStates = null)
        {
            FlowId=id;PlanId=planId;Metadata=metadata;State=state;
            CancelRequested=cancelRequested;CreatedUtc=createdUtc;EndedUtc=endedUtc;
            TotalSteps=total;CompletedSteps=completed;WaitingSteps=waiting;
            FailedSteps=failed;SkippedSteps=skipped;
            RegionStates=new ReadOnlyDictionary<string,MRKStepState>(
                new Dictionary<string,MRKStepState>(regionStates ??
                    new Dictionary<string,MRKStepState>(),StringComparer.Ordinal));
        }
        public Guid FlowId {get;private set;}
        public string PlanId {get;private set;}
        public MRKFlowMetadata Metadata {get;private set;}
        public MRKFlowState State {get;private set;}
        public bool CancelRequested {get;private set;}
        public DateTime CreatedUtc {get;private set;}
        public DateTime? EndedUtc {get;private set;}
        public int TotalSteps {get;private set;}
        public int CompletedSteps {get;private set;}
        public int WaitingSteps {get;private set;}
        public int FailedSteps {get;private set;}
        public int SkippedSteps {get;private set;}
        public IDictionary<string,MRKStepState> RegionStates{get;private set;}
    }

}
