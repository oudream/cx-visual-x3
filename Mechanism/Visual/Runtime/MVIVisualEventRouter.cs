using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>可视事件转交已有专业/MRK命令Owner的只读请求，不包含设备操作或权限绕过。</summary>
    public sealed class MVIVisualActionInvocation
    {
        internal MVIVisualActionInvocation(Guid itemId,string eventId,string actionId,
            string bindingId,string value)
        {
            ItemId=itemId;EventId=eventId;ActionId=actionId;
            ParameterBindingId=bindingId;EventValue=value;
        }
        public Guid ItemId{get;private set;}
        public string EventId{get;private set;}
        public string ActionId{get;private set;}
        public string ParameterBindingId{get;private set;}
        public string EventValue{get;private set;}
    }
    /// <summary>仅解析已保存的行为定义；没有显式授权的宿主处理器时绝不执行动作。</summary>
    public static class MVIVisualEventRouter
    {
        public static IReadOnlyList<MVIVisualActionInvocation> Resolve(
            MEMEngineeringSession document,Guid elementId,string eventId,string value)
        {
            if(document==null)throw new ArgumentNullException("document");
            if(string.IsNullOrWhiteSpace(eventId))throw new ArgumentException("eventId");
            var result=new List<MVIVisualActionInvocation>();
            MRCVisualItem item=document.Find(elementId) as MRCVisualItem;
            if(item==null||!item.IsVisible)return result.AsReadOnly();
            foreach(MRCVisualEventAction action in MRCVisualAppearance.BehaviorOf(item).EventActions)
            {
                if(!string.Equals(action.EventId,eventId,StringComparison.Ordinal))continue;
                result.Add(new MVIVisualActionInvocation(item.Id,eventId,
                    action.ActionId,action.ParameterBindingId,value));
            }
            return result.AsReadOnly();
        }
        public static int Dispatch(MEMEngineeringSession document,Guid elementId,
            string eventId,string value,Func<MVIVisualActionInvocation,bool> authorizedHost)
        {
            if(authorizedHost==null)return 0; // fail-closed，不能从画图预览直接下发设备命令
            IReadOnlyList<MVIVisualActionInvocation> actions=Resolve(
                document,elementId,eventId,value);
            int accepted=0;
            foreach(MVIVisualActionInvocation request in actions)
                if(authorizedHost(request))accepted++;
            return accepted;
        }
    }
}
