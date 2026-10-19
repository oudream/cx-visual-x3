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
            :this(itemId,null,eventId,actionId,bindingId,value){}
        internal MVIVisualActionInvocation(Guid itemId,MRIObjectAddress address,
            string eventId,string actionId,string bindingId,string value)
        {
            ItemId=itemId;Address=address;EventId=eventId;ActionId=actionId;
            ParameterBindingId=bindingId;EventValue=value;
        }
        /// <summary>被触发的实例地址，不是共享定义的扁平Guid。</summary>
        public MRIObjectAddress Address{get;private set;}
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
            if(string.IsNullOrWhiteSpace(eventId)||eventId.Length>512)
                throw new ArgumentException("visual.event.invalid_id");
            if(value!=null&&value.Length>65536)
                throw new ArgumentException("visual.event.payload_budget");
            var result=new List<MVIVisualActionInvocation>();
            MRCVisualItem item=document.Find(elementId) as MRCVisualItem;
            if(item==null)return result.AsReadOnly();
            for(MEMNode parent=item;parent!=null;parent=parent.Parent)
            {
                MRCVisualItem visual=parent as MRCVisualItem;
                if(visual!=null&&!visual.IsVisible)return result.AsReadOnly();
            }
            foreach(MRCVisualEventAction action in MRCVisualAppearance.BehaviorOf(item).EventActions)
            {
                if(!string.Equals(action.EventId,eventId,StringComparison.Ordinal))continue;
                result.Add(new MVIVisualActionInvocation(item.Id,eventId,
                    action.ActionId,action.ParameterBindingId,value));
            }
            return result.AsReadOnly();
        }
        /// <summary>Symbol实例Widget必须按完整地址路由；共享定义只读，真正授权仍由宿主负责。</summary>
        public static IReadOnlyList<MVIVisualActionInvocation> Resolve(
            MEMEngineeringSession document,MRIObjectAddress address,string eventId,string value)
        {
            if(document==null)throw new ArgumentNullException("document");
            if(address==null)throw new ArgumentNullException("address");
            if(string.IsNullOrWhiteSpace(eventId)||eventId.Length>512)
                throw new ArgumentException("visual.event.invalid_id");
            if(value!=null&&value.Length>65536)
                throw new ArgumentException("visual.event.payload_budget");
            // MEM 原Owner校验项目身份、资源归属和逐层实例引用。
            MRCVisualItem item=document.Resolve(address) as MRCVisualItem;
            var result=new List<MVIVisualActionInvocation>();
            if(item==null)return result.AsReadOnly();
            for(MEMNode parent=item;parent!=null;parent=parent.Parent)
            {
                MRCVisualItem visual=parent as MRCVisualItem;
                if(visual!=null&&!visual.IsVisible)return result.AsReadOnly();
            }
            foreach(MRCVisualEventAction action in MRCVisualAppearance.BehaviorOf(item).EventActions)
                if(string.Equals(action.EventId,eventId,StringComparison.Ordinal))
                    result.Add(new MVIVisualActionInvocation(item.Id,address,eventId,
                        action.ActionId,action.ParameterBindingId,value));
            return result.AsReadOnly();
        }
        public static int Dispatch(MEMEngineeringSession document,MRIObjectAddress address,
            string eventId,string value,Func<MVIVisualActionInvocation,bool> authorizedHost)
        {
            if(authorizedHost==null)return 0; // 默认禁止预览态直接控制设备
            int accepted=0;
            foreach(MVIVisualActionInvocation request in Resolve(document,address,eventId,value))
                if(authorizedHost(request))accepted++;
            return accepted;
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
