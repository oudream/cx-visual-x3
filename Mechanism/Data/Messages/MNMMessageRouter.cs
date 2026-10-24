using System;
using System.Collections.Generic;
using System.Linq;

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

        internal MNMMessageSubscription[] Resolve(IList<MNMMessageSubscription> subscriptions,MNMMessage message)
        {
            var normal=new List<MNMMessageSubscription>();
            var shared=new Dictionary<string,List<MNMMessageSubscription>>(StringComparer.Ordinal);
            var already=new HashSet<string>(StringComparer.Ordinal);
            foreach(MNMMessageSubscription s in subscriptions)
            {
                if(!s.IsActive || !s.TopicFilter.Matches(message.Header.Topic) ||
                    !s.PayloadType.IsAssignableFrom(message.Payload.PayloadType))
                    continue;
                if (s.Options.NoLocal && s.Options.SourceId == message.Header.SourceId)
                    continue;
                if (!string.IsNullOrWhiteSpace(s.Options.SourceFilter) &&
                    !string.Equals(s.Options.SourceFilter,message.Header.SourceId,StringComparison.Ordinal))
                    continue;
                if (!string.IsNullOrWhiteSpace(s.Options.TypeId) &&
                    s.Options.TypeId != message.Header.TypeId)
                    continue;
                // TargetId 精确对应 SubscriberId；不得把点对点默默广播给其它订阅者。
                if (!string.IsNullOrWhiteSpace(message.Header.TargetId) &&
                    !string.Equals(s.SubscriberId,message.Header.TargetId,StringComparison.Ordinal))
                    continue;
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
