using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>
    /// 软件Plan的工程发布候选，锁定创建时的工程Revision、引用Content和Host声明的能力版本。
    /// 本指纹只证明受限Logic来源一致，不是整包签名，也不是设备侧互锁或发布ACK。
    /// </summary>
    public sealed class MOCProjectReleaseCandidate
    {
        internal MOCProjectReleaseCandidate(MOCCompiledPlan plan,Guid entry,
            string fingerprint,IDictionary<string,string> dependencies)
        {
            Plan=plan;EntryContentId=entry;Fingerprint=fingerprint;
            CapabilityVersions=new ReadOnlyDictionary<string,string>(
                new Dictionary<string,string>(dependencies,StringComparer.Ordinal));
        }
        public MOCCompiledPlan Plan{get;private set;}
        public Guid ProjectId{get{return Plan.ProjectId;}}
        public int EngineeringRevision{get{return Plan.EngineeringRevision;}}
        public Guid EntryContentId{get;private set;}
        public string Fingerprint{get;private set;}
        public IReadOnlyDictionary<string,string> CapabilityVersions{get;private set;}
    }

    /// <summary>
    /// 不新建Publish/Worker体系，MOC只核对工程依赖，实际授权Stage/Activate仍归MRK。
    /// 专业Provider和设备必须通过Host另行提供可信版本、安全点与实际反馈。
    /// </summary>
    public static class MOCProjectRelease
    {
        private static void Gather(MEMProjectSnapshot snapshot,Guid contentId,
            IReadOnlyDictionary<string,string> declared,
            SortedDictionary<string,string> capabilities,
            SortedDictionary<string,string> sources,HashSet<Guid> visiting)
        {
            if(!visiting.Add(contentId))
                throw new InvalidOperationException("moc.release.recursive_content");
            try
            {
                MEMNodeSnapshot node=snapshot.Find(contentId);
                if(node==null||node.Kind!=MEMNodeKind.Content)
                    throw new InvalidOperationException("moc.release.content_not_found");
                MOLWiringDefinition wiring=MOLWiringCodec.Get(node);
                if(wiring==null)throw new InvalidOperationException("moc.release.requires_v2");
                wiring.ValidateExecutable();
                string address=contentId.ToString("N");
                if(sources.ContainsKey(address))return; // 同一个Content可被多次调用
                sources.Add(address,
                    Convert.ToBase64String(MOLWiringCodec.Serialize(wiring)));
                foreach(MOLControlBlock block in wiring.Blocks)
                {
                    string capability=block.CapabilityId;
                    if(capability=="moc.state-machine")continue;
                    if(capability.StartsWith("moc.invoke:",StringComparison.Ordinal))
                    {
                        Guid target;
                        if(!Guid.TryParseExact(capability.Substring(11),"N",out target)||
                            !node.References.Any(r=>r.Kind==MRIReferenceKind.Content&&
                                r.TargetId==target))
                            throw new InvalidOperationException("moc.release.invalid_content_ref");
                        Gather(snapshot,target,declared,capabilities,sources,visiting);
                        continue;
                    }
                    string version;
                    if(!declared.TryGetValue(capability,out version)||
                        string.IsNullOrWhiteSpace(version)||version.Length>128||
                        version.Any(char.IsControl))
                        throw new InvalidOperationException("moc.release.capability_version_missing:"+capability);
                    string old;
                    if(capabilities.TryGetValue(capability,out old)&&old!=version)
                        throw new InvalidOperationException("moc.release.version_conflict");
                    capabilities[capability]=version;
                }
            }
            finally{visiting.Remove(contentId);}
        }

        private static string ComputeFingerprint(MEMProjectSnapshot snapshot,Guid contentId,
            IReadOnlyDictionary<string,string> declared,
            out IDictionary<string,string> pinned)
        {
            if(snapshot==null||declared==null)
                throw new ArgumentNullException("snapshot/declared");
            var capabilities=new SortedDictionary<string,string>(StringComparer.Ordinal);
            var contents=new SortedDictionary<string,string>(StringComparer.Ordinal);
            Gather(snapshot,contentId,declared,capabilities,contents,new HashSet<Guid>());
            var value=new StringBuilder().Append("moc-release-logic-v1\n")
                .Append(snapshot.ProjectId.ToString("N")).Append('|')
                .Append(snapshot.Revision.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(contentId.ToString("N")).Append('\n');
            foreach(var pair in contents)
                value.Append("C|").Append(pair.Key).Append('|').Append(pair.Value).Append('\n');
            foreach(var pair in capabilities)
                value.Append("A|").Append(pair.Key.Length).Append(':').Append(pair.Key)
                    .Append('|').Append(pair.Value.Length).Append(':').Append(pair.Value).Append('\n');
            using(var sha=SHA256.Create())
            {
                pinned=new Dictionary<string,string>(capabilities,StringComparer.Ordinal);
                return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value.ToString()))).Replace("-","");
            }
        }

        public static MOCProjectReleaseCandidate Prepare(MEMProjectSnapshot snapshot,
            Guid entryContentId,IMRKCapabilityResolver resolver,
            IReadOnlyDictionary<string,string> declaredVersions)
        {
            if(resolver==null)throw new ArgumentNullException("resolver");
            IDictionary<string,string> pinned;
            string fingerprint=ComputeFingerprint(snapshot,entryContentId,declaredVersions,out pinned);
            MOCBuildResult build=new MOCBuildService().Build(snapshot,entryContentId,resolver);
            if(!build.Succeeded)
                throw new InvalidOperationException("moc.release.build_rejected:"+
                    string.Join(";",build.Diagnostics.Select(d=>d.Code)));
            return new MOCProjectReleaseCandidate(build.Plan,entryContentId,fingerprint,pinned);
        }

        /// <summary>
        /// Stage前重新核对所有可达Content与声明能力版本；不匹配不污染MRK PlanSlot。
        /// newPublicationRevision是发布修订号，与工程Revision分别计数。
        /// </summary>
        public static MRKPlanPublicationReceipt Stage(MOCProjectReleaseCandidate candidate,
            MEMProjectSnapshot current,IMRKCapabilityResolver resolver,
            IReadOnlyDictionary<string,string> declaredVersions,
            MRKExecutionPlanPublisher publisher,string principal,long newPublicationRevision)
        {
            if(candidate==null||publisher==null||resolver==null)
                throw new ArgumentNullException("candidate/publisher/resolver");
            if(current==null||current.ProjectId!=candidate.ProjectId||
                current.Revision!=candidate.EngineeringRevision)
                throw new InvalidOperationException("moc.release.stale_engineering_revision");
            IDictionary<string,string> actual;
            string fingerprint=ComputeFingerprint(current,candidate.EntryContentId,
                declaredVersions,out actual);
            if(!string.Equals(fingerprint,candidate.Fingerprint,StringComparison.Ordinal)||
                actual.Count!=candidate.CapabilityVersions.Count||
                actual.Any(x=>!candidate.CapabilityVersions.ContainsKey(x.Key)||
                    candidate.CapabilityVersions[x.Key]!=x.Value))
                throw new InvalidOperationException("moc.release.dependency_changed");
            MOCBuildResult fresh=new MOCBuildService().Build(current,
                candidate.EntryContentId,resolver);
            if(!fresh.Succeeded||fresh.Plan.Plan.Id!=candidate.Plan.Plan.Id)
                throw new InvalidOperationException("moc.release.plan_no_longer_valid");
            return publisher.StagePrecompiled(principal,candidate.Plan.Plan,newPublicationRevision);
        }
    }
}
