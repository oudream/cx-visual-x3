using System;
using CxVisual.Mechanism.Visual;
namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>WPF世界坐标编辑手势，承载真实选中的Handle，工程修改交由Visual/MEM。</summary>
    public sealed class MPWVisualHandleEventArgs : EventArgs
    {
        public MPWVisualHandleEventArgs(MVIVisualHandle handle,double worldX,double worldY)
        {Handle=handle??throw new ArgumentNullException("handle");WorldX=worldX;WorldY=worldY;}
        public MVIVisualHandle Handle{get;private set;}
        public double WorldX{get;private set;}
        public double WorldY{get;private set;}
    }
}
