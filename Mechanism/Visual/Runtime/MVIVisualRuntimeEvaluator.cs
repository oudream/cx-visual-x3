using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>运行态显示只从已有 MDS/专业 Owner 读取，不创建第二个数据仓库；永不写入工程。</summary>
    public static class MVIVisualRuntimeEvaluator
    {
        public static MPMValue Resolve(MPMBinding binding,MPMValueKind target,
            IMDSExternalBindingResolver source)
        {
            if(binding==null)return null;
            if(binding.Kind==MPMBindingKind.Literal)return binding.Value;
            if(source==null)return null; // 设计态无外部运行上下文时使用工程默认值
            MDSBindingSource kind;
            switch(binding.Kind)
            {
                case MPMBindingKind.Variable:kind=MDSBindingSource.Variable;break;
                case MPMBindingKind.Expression:kind=MDSBindingSource.Expression;break;
                default:return null; // Config/Resource/Member 继续交由各自已存在的 Owner
            }
            Type type=target==MPMValueKind.Boolean?typeof(bool):
                target==MPMValueKind.Number?typeof(double):
                target==MPMValueKind.String?typeof(string):null;
            if(type==null)throw new InvalidOperationException("visual.runtime.value_kind_not_supported");
            string key=binding.SourceId;
            if(binding.Kind==MPMBindingKind.Variable&&
                !string.IsNullOrEmpty(binding.MemberId)&&binding.MemberId!="value")
                key=key+"."+binding.MemberId;
            object value=source.Resolve(kind,key,type);
            if(value==null)return null; // 未加载、无当前值：不伪造数据
            if(type==typeof(bool)&&value is bool)return MPMValue.Boolean((bool)value);
            if(type==typeof(double)&&value is double)
                return MPMValue.Number((double)value);
            if(type==typeof(string)&&value is string)
                return MPMValue.String((string)value);
            throw new InvalidCastException("visual.runtime.binding_value_kind_mismatch");
        }
        public static bool StateActive(string trigger,IMDSExternalBindingResolver source)
        {
            if(source==null||string.IsNullOrEmpty(trigger))return false;
            object value=source.Resolve(MDSBindingSource.Variable,trigger,typeof(bool));
            if(value==null)return false;
            if(!(value is bool))throw new InvalidCastException("visual.runtime.state_requires_boolean");
            return (bool)value;
        }
        /// <summary>运行时动画采样：属性只在当前Scene帧变化，不回写MEM声明。</summary>
        public static double EvaluateNumber(string property,double declared,
            MRCVisualBehaviorDefinition behavior,double elapsedMilliseconds,
            IMDSExternalBindingResolver source)
        {
            if(behavior==null)return declared;
            if(double.IsNaN(elapsedMilliseconds)||double.IsInfinity(elapsedMilliseconds)||
                elapsedMilliseconds<0)throw new ArgumentOutOfRangeException("elapsedMilliseconds");
            double value=declared;
            foreach(MRCAnimationDefinition animation in behavior.Animations)
            {
                if(!string.Equals(animation.Property,property,StringComparison.OrdinalIgnoreCase))
                    continue;
                if(animation.TriggerId.Length>0&&!StateActive(animation.TriggerId,source))
                    continue;
                double t=elapsedMilliseconds/animation.DurationMilliseconds;
                if(animation.Repeat)t=t-Math.Floor(t);
                else t=Math.Min(1,t);
                switch(animation.Easing)
                {
                    case MRCAnimationEasing.EaseIn:t=t*t;break;
                    case MRCAnimationEasing.EaseOut:t=1-(1-t)*(1-t);break;
                    case MRCAnimationEasing.EaseInOut:t=t*t*(3-2*t);break;
                }
                value=animation.From+(animation.To-animation.From)*t;
                if(double.IsNaN(value)||double.IsInfinity(value))
                    throw new InvalidOperationException("visual.animation.overflow");
            }
            return value;
        }
        public static MRCVisualStyle EvaluateStyle(MRCVisualStyle declared,
            MRCVisualBehaviorDefinition behavior,double elapsedMilliseconds,
            IMDSExternalBindingResolver source)
        {
            if(declared==null)throw new ArgumentNullException("declared");
            if(behavior==null)return declared;
            if(double.IsNaN(elapsedMilliseconds)||double.IsInfinity(elapsedMilliseconds)||
                elapsedMilliseconds<0)throw new ArgumentOutOfRangeException("elapsedMilliseconds");
            MRCVisualStyle style=declared;
            foreach(MRCVisualStateDefinition state in behavior.States)
                if(StateActive(state.Name,source))style=state.Style;
            double opacity=style.Opacity;
            double strokeWidth=style.StrokeWidth;
            foreach(MRCAnimationDefinition animation in behavior.Animations)
            {
                bool opacityAnimation=string.Equals(animation.Property,"opacity",StringComparison.OrdinalIgnoreCase);
                bool widthAnimation=string.Equals(animation.Property,"strokeWidth",StringComparison.OrdinalIgnoreCase);
                if(!opacityAnimation&&!widthAnimation)continue;
                if(animation.TriggerId.Length>0&&!StateActive(animation.TriggerId,source))continue;
                double t=elapsedMilliseconds/animation.DurationMilliseconds;
                if(animation.Repeat)t=t-Math.Floor(t);
                else t=Math.Min(1,t);
                switch(animation.Easing)
                {
                    case MRCAnimationEasing.EaseIn:t=t*t;break;
                    case MRCAnimationEasing.EaseOut:t=1-(1-t)*(1-t);break;
                    case MRCAnimationEasing.EaseInOut:t=t*t*(3-2*t);break;
                }
                double value=animation.From+(animation.To-animation.From)*t;
                if(opacityAnimation)opacity=Math.Max(0,Math.Min(1,value));
                else strokeWidth=Math.Max(0,value);
            }
            if(opacity==style.Opacity&&strokeWidth==style.StrokeWidth)return style;
            return new MRCVisualStyle(style.Fill,style.Stroke,strokeWidth,
                opacity,style.FontFamily,style.FontSize,style.ThemeKey);
        }
    }
}
