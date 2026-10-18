using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public sealed class MRIObjectHandle
    {
        internal MRIObjectHandle(Guid session,Guid project,Guid objectId) {SessionId=session;ProjectId=project;ObjectId=objectId;}
        internal Guid SessionId { get; private set; }
        public Guid ProjectId { get; private set; }
        public Guid ObjectId { get; private set; }
    }
    /// <summary>Definition address plus ordered usage IDs. Display names and runtime instance IDs are never identity.</summary>
    public sealed class MRIObjectAddress : IEquatable<MRIObjectAddress>
    {
        public MRIObjectAddress(Guid projectId,Guid resourceId,Guid objectId,IEnumerable<Guid> instancePath=null)
        {
            if(projectId==Guid.Empty||resourceId==Guid.Empty||objectId==Guid.Empty)throw new ArgumentException("Nonempty address identities required");
            ProjectId=projectId;ResourceId=resourceId;ObjectId=objectId;
            var path=instancePath==null?new Guid[0]:instancePath.Take(MEMNode.MaximumDepth+1).ToArray();
            if(path.Length>MEMNode.MaximumDepth||path.Any(id=>id==Guid.Empty))throw new ArgumentException("Invalid instance path");
            InstancePath=Array.AsReadOnly(path);
        }
        public Guid ProjectId { get; private set; }
        public Guid ResourceId { get; private set; }
        public Guid ObjectId { get; private set; }
        public IReadOnlyList<Guid> InstancePath { get; private set; }
        public override string ToString()
        {return "cx://"+ProjectId.ToString("N")+"/"+ResourceId.ToString("N")+"/"+ObjectId.ToString("N")+(InstancePath.Count==0?"":"?via="+string.Join(".",InstancePath.Select(id=>id.ToString("N"))));}
        public static MRIObjectAddress Parse(string text)
        {MRIObjectAddress value;if(!TryParse(text,out value))throw new FormatException("Invalid engineering address");return value;}
        public static bool TryParse(string text,out MRIObjectAddress address)
        {
            address=null;if(text==null||text.Length>10000||!text.StartsWith("cx://",StringComparison.Ordinal))return false;
            var parts=text.Substring(5).Split('?');if(parts.Length>2)return false;
            var ids=parts[0].Split('/');Guid p,r,o;
            if(ids.Length!=3||!Guid.TryParseExact(ids[0],"N",out p)||!Guid.TryParseExact(ids[1],"N",out r)||!Guid.TryParseExact(ids[2],"N",out o))return false;
            var path=new List<Guid>();
            if(parts.Length==2)
            {
                if(!parts[1].StartsWith("via=",StringComparison.Ordinal))return false;
                foreach(var token in parts[1].Substring(4).Split('.')){Guid id;if(!Guid.TryParseExact(token,"N",out id))return false;path.Add(id);}
            }
            try{address=new MRIObjectAddress(p,r,o,path);return true;}catch(ArgumentException){return false;}
        }
        public bool Equals(MRIObjectAddress other)
        {return other!=null&&ProjectId==other.ProjectId&&ResourceId==other.ResourceId&&ObjectId==other.ObjectId&&InstancePath.SequenceEqual(other.InstancePath);}
        public override bool Equals(object value) {return Equals(value as MRIObjectAddress);}
        public override int GetHashCode() {unchecked{int hash=ProjectId.GetHashCode()*397^ResourceId.GetHashCode();hash=hash*397^ObjectId.GetHashCode();foreach(var id in InstancePath)hash=hash*397^id.GetHashCode();return hash;}}
    }
}
