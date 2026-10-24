using System;
namespace CxVisual.Foundation.Core
{
    /// <summary>不可变运行端点地址；不替代MEM工程对象MRIObjectAddress。</summary>
    public sealed class FOIInteractionEndpointAddress
    {
        public FOIInteractionEndpointAddress(string path,long numericId,Guid runId,long generation)
        {
            Path=NormalizePath(path);
            if(numericId<=0)throw new ArgumentOutOfRangeException("numericId");
            if(runId==Guid.Empty)throw new ArgumentException("无效RunId","runId");
            if(generation<=0)throw new ArgumentOutOfRangeException("generation");
            NumericId=numericId;RunId=runId;Generation=generation;
        }
        public string Path{get;private set;}
        public long NumericId{get;private set;}
        public Guid RunId{get;private set;}
        public long Generation{get;private set;}
        public string Kind{get{return Path.Split('/')[7];}}
        public static string NormalizePath(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||path.Length>512||path[0]!='/')
                throw new ArgumentException("无效端点路径","path");
            string[] s=path.Substring(1).Split('/');
            if(s.Length!=8||(s[0]!="machine"&&s[0]!="container")||
                s[2]!="app"||s[4]!="module")
                throw new ArgumentException("端点路径必须为机器或容器/app/module/kind/code八段","path");
            foreach(string part in s)
            {
                if(part.Length==0||part.Length>64||part=="."||part=="..")
                    throw new ArgumentException("无效路径段","path");
                foreach(char c in part)
                    if(!((c>='A'&&c<='Z')||(c>='a'&&c<='z')||
                         (c>='0'&&c<='9')||c=='_'||c=='-'||c=='.'))
                        throw new ArgumentException("路径段不能包含通配符/未规范化字符","path");
            }
            return path;
        }
    }
}
