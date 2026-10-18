using System;
using System.Threading;

namespace CxVisual.Mechanism.Core
{
    public enum MEMAccessOperation {Open=1,Read=2,Edit=3,Save=4,Export=5,PrepareRun=6}
    public interface IMEMAccessPolicy { bool IsAllowed(MEMAccessRequest request); }
    public sealed class MEMAccessRequest
    {
        internal MEMAccessRequest(MEMAccessOperation operation,Guid projectId,string location){Operation=operation;ProjectId=projectId;Location=location;}
        public MEMAccessOperation Operation {get;private set;} public Guid ProjectId {get;private set;} public string Location {get;private set;}
    }
    public enum MEMCloseMode {RequireSaved=1,Discard=2,Save=3}
    public sealed partial class MEMEngineeringSession
    {
        private readonly object _policyGate=new object();
        private IMEMAccessPolicy _accessPolicy;
        private int _authorizing;
        private int _policyThread;
        internal void UseAccessPolicy(IMEMAccessPolicy policy){_accessPolicy=policy;}
        // Operation gate, not a sandbox for code already holding live identities or immutable snapshots.
        // Internal calls in the authorized edit/read critical section reuse the admission decision.
        internal void DemandAccess(MEMAccessOperation operation,string location=null)
        {
            if(_accessPolicy==null||Monitor.IsEntered(_gate))return;
            lock(_policyGate)
            {
                if(_policyThread==Thread.CurrentThread.ManagedThreadId)throw new InvalidOperationException("org.access.policy_reentrant");
                _policyThread=Thread.CurrentThread.ManagedThreadId;Interlocked.Increment(ref _authorizing);
                try{if(!_accessPolicy.IsAllowed(new MEMAccessRequest(operation,_project.Id,location??Location)))throw new UnauthorizedAccessException("Engineering operation denied: "+operation);}
                finally{Interlocked.Decrement(ref _authorizing);_policyThread=0;}
            }
        }
        public void Close(MEMCloseMode mode)
        {
            if(!Enum.IsDefined(typeof(MEMCloseMode),mode))throw new ArgumentOutOfRangeException("mode");
            if(mode==MEMCloseMode.Save){if(IsDirty)Save();Close();}else Close(mode==MEMCloseMode.Discard);
        }
        public MEMSaveSnapshot CaptureSaveSnapshot()
        {
            DemandAccess(MEMAccessOperation.Export);
            lock(_gate){DemandOpen();if(_editing||Volatile.Read(ref _collectingTypes)!=0||Volatile.Read(ref _authorizing)!=0)throw new InvalidOperationException("org.snapshot.busy");return MEMFileProjectStore.Capture(this,CancellationToken.None);}
        }
    }
}
