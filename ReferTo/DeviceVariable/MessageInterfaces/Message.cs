using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageInterfaces
{
    public enum MessageType
    {
        Error,
        Warning,
        Info,
        Command,
    }

    public struct Message
    {
        public string Topic;
        public string Body; // 简化为字符串
        public long Timestamp; // 时间戳,单位为毫秒
        public long Source;
        public long Target; // 
        public long ReservedInt1;
        public long ReservedInt2;
        public string ReservedString1;
        public string ReservedString2;
    }

}
