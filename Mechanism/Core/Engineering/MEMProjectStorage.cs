using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Core
{
    public interface IMEMProjectStore
    {
        MEMEngineeringSession Load(string path);
        MEMSaveResult Save(MEMSaveSnapshot snapshot,string path,string expectedToken,CancellationToken cancellation);
    }
    /// <summary>Frozen encoded definition. No live objects, runtime state, callbacks or writable object tree.</summary>
    public sealed class MEMSaveSnapshot
    {
        private readonly byte[] _xml;
        internal MEMSaveSnapshot(Guid projectId,int revision,byte[] xml,System.Collections.Generic.IReadOnlyList<MEMAssetReference> assets){ProjectId=projectId;Revision=revision;_xml=xml;Assets=assets;}
        public Guid ProjectId {get;private set;} public int Revision {get;private set;}
        public System.Collections.Generic.IReadOnlyList<MEMAssetReference> Assets {get;private set;}
        public Stream OpenRead(){return new MemoryStream(_xml,0,_xml.Length,false,false);}
    }
    public sealed class MEMSaveResult
    {
        public MEMSaveResult(Guid projectId,int savedRevision,string location,string storeToken)
        {if(projectId==Guid.Empty||savedRevision<0||string.IsNullOrWhiteSpace(storeToken))throw new ArgumentException("Invalid save receipt");ProjectId=projectId;SavedRevision=savedRevision;Location=Path.GetFullPath(location);StoreToken=storeToken;}
        public Guid ProjectId {get;private set;} public int SavedRevision {get;private set;} public string Location {get;private set;} public string StoreToken {get;private set;}
    }
    public sealed class MEMStoreConflictException : IOException
    {public MEMStoreConflictException(string path):base("Engineering store changed or target already exists: "+path){}}
    public sealed partial class MEMEngineeringSession
    {
        private IMEMProjectStore _store=MEMFileProjectStore.Default;
        private bool _saving;
        public string StoreToken {get;private set;}
        public MEMSaveResult LastSaveResult {get;private set;}
        internal void UseStore(IMEMProjectStore store){_store=store??MEMFileProjectStore.Default;}
        private MEMSaveSnapshot CaptureSave(string path,CancellationToken cancellation,out string full,out string expected)
        {
            DemandAccess(MEMAccessOperation.Save,path);
            lock(_gate)
            {
                DemandOpen();if(_editing||_saving||Volatile.Read(ref _collectingTypes)!=0||Volatile.Read(ref _authorizing)!=0)throw new InvalidOperationException("org.save.busy");
                cancellation.ThrowIfCancellationRequested();full=Path.GetFullPath(path);expected=string.Equals(Location,full,StringComparison.OrdinalIgnoreCase)?StoreToken:null;
                var snapshot=MEMFileProjectStore.Capture(this,cancellation);_saving=true;return snapshot;
            }
        }
        private void SaveCaptured(MEMSaveSnapshot snapshot,string path,string expected,CancellationToken cancellation)
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();var result=_store.Save(snapshot,path,expected,cancellation);
                if(result==null||result.ProjectId!=snapshot.ProjectId||result.SavedRevision!=snapshot.Revision||!string.Equals(result.Location,path,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid store receipt");
                lock(_gate){Location=result.Location;StoreToken=result.StoreToken;_savedRevision=result.SavedRevision;LastSaveResult=result;}
            }
            finally{lock(_gate)_saving=false;}
        }
        public Task SaveAsAsync(string path,CancellationToken cancellation)
        {
            string full,expected;var snapshot=CaptureSave(path,cancellation,out full,out expected);
            try{return Task.Run(()=>SaveCaptured(snapshot,full,expected,cancellation));}
            catch{lock(_gate)_saving=false;throw;}
        }
    }
}
