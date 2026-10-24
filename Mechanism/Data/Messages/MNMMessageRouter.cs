using System;
using System.Collections.Generic;
using System.Linq;

using CxVisual.Foundation.Data.Messages;

namespace CxVisual.Mechanism.Data.Messages
{
    /// <summary>
    /// 进程内主题路由。Observer一对多、共享组竞争消费和点对点均由这一处裁决。
    /// 由MessageCenter在锁下调用，不保存第二份Subscription Registry。
    /// </summary>
    internal sealed class MNMMessageRouter
    {
        private readonly Dictionary<string,int> _roundRobin =
            new Dictionary<string,int>(StringComparer.Ordinal);

        /// <summary>正常Publish与Retain replay共用过滤规则，避免历史定向/来源消息越界重放。</summary>
        internal static bool MatchesSubscription(MNMMessageSubscription s,MNMMessage message,string[] validatedTopicSegments = null)
        {
            if (!s.IsActive ||
                !(validatedTopicSegments == null
                    ? s.TopicFilter.Matches(message.Header.Topic)
                    : s.TopicFilter.MatchesValidatedSegments(validatedTopicSegments)) ||
                !s.PayloadType.IsAssignableFrom(message.Payload.PayloadType))
                return false;
            if (s.Options.NoLocal &&
                !string.IsNullOrWhiteSpace(s.Options.SourceId) &&
                string.Equals(s.Options.SourceId,message.Header.SourceId,StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(s.Options.SourceFilter) &&
                !string.Equals(s.Options.SourceFilter,message.Header.SourceId,StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(s.Options.TypeId) &&
                !string.Equals(s.Options.TypeId,message.Header.TypeId,StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(message.Header.TargetId) &&
                !string.Equals(s.SubscriberId,message.Header.TargetId,StringComparison.Ordinal))
                return false;
            return true;
        }

        internal MNMMessageSubscription[] Resolve(IList<MNMMessageSubscription> subscriptions,MNMMessage message)
        {
            // MessageHeader已经验证了具体Topic，避免每位订阅者都重新Split与Validate。
            string[] segments=message.Header.Topic.Substring(1).Split('/');
            var normal=new List<MNMMessageSubscription>();
            var shared=new Dictionary<string,List<MNMMessageSubscription>>(StringComparer.Ordinal);
            var already=new HashSet<string>(StringComparer.Ordinal);
            foreach(MNMMessageSubscription s in subscriptions)
            {
                if (!MatchesSubscription(s,message,segments)) continue;
                if(s.SharedGroup == null || !string.IsNullOrWhiteSpace(message.Header.TargetId))
                {
                    if(already.Add(s.SubscriberId)) normal.Add(s);
                }
                else
                {
                    string key=s.SharedGroup+"|"+s.Filter;
                    List<MNMMessageSubscription> members;
                    if(!shared.TryGetValue(key,out members))
                    {members=new List<MNMMessageSubscription>();shared.Add(key,members);}
                    members.Add(s);
                }
            }
            foreach(var group in shared.OrderBy(v=>v.Key,StringComparer.Ordinal))
            {
                int index;
                if(!_roundRobin.TryGetValue(group.Key,out index))index=0;
                normal.Add(group.Value[index % group.Value.Count]);
                _roundRobin[group.Key]=(index+1)%group.Value.Count;
            }
            return normal.ToArray();
        }
    }
}
