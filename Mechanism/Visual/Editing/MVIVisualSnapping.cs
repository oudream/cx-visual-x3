using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 一次视觉拖动的纯计算结果；参考线是只读显示信息，不进入MEM工程文件。
    /// 世界位移始终用于工程提交，WPF只负责换算屏幕输入与显示辅助线。
    /// </summary>
    public sealed class MVIVisualSnapResult
    {
        public MVIVisualSnapResult(double dx,double dy,double? guideX,double? guideY)
        {
            WorldDx=dx;WorldDy=dy;GuideX=guideX;GuideY=guideY;
        }
        public double WorldDx {get;private set;}
        public double WorldDy {get;private set;}
        public double? GuideX {get;private set;}
        public double? GuideY {get;private set;}
    }

    /// <summary>
    /// 统一图元/Group选区的世界坐标吸附。仅依赖一次场景投影，不保存状态、不访问WPF、
    /// 不创建第二套选择/Undo。智能参考线优先于网格；没有候选时才使用25单位网格。
    /// 随Zoom缩放的6 DIP容差保证高DPI/画布缩放下命中尺度一致。
    /// </summary>
    public static class MVIVisualSnapping
    {
        public static MVIVisualSnapResult Resolve(IReadOnlyList<MVISceneItem> scene,
            IEnumerable<Guid> movingIds,double dx,double dy,double zoom,
            bool snapGrid,bool snapObjects)
        {
            if(scene==null)throw new ArgumentNullException("scene");
            if(movingIds==null)throw new ArgumentNullException("movingIds");
            FGEValidation.Finite(dx,"dx");FGEValidation.Finite(dy,"dy");
            if(zoom<=0||double.IsNaN(zoom)||double.IsInfinity(zoom))
                throw new ArgumentOutOfRangeException("zoom");

            HashSet<Guid> selected=new HashSet<Guid>(movingIds);
            MVISceneItem[] moving=scene.Where(item=>selected.Contains(item.Id)&&
                item.IsSelectable&&item.ElementTypeId!="core.visual.connector").ToArray();
            if(moving.Length==0||moving.Length!=selected.Count)
                throw new ArgumentException("visual.snap.invalid_selection","movingIds");
            FGERect2D bounds=MVISelectionTransform.Bounds(moving);
            double? guideX=null,guideY=null;
            double xCorrection=0,yCorrection=0;
            double bestX=6/zoom,bestY=6/zoom;
            if(snapObjects)
            {
                // 每轴分别比较左/中/右、上/中/下；相同距离沿场景遍历顺序确定，
                // 不生成临时持久化锚点；选区内部和视觉连接线均不作为吸附目标。
                double[] xs={bounds.Left,bounds.Left+bounds.Width/2,bounds.Right};
                double[] ys={bounds.Top,bounds.Top+bounds.Height/2,bounds.Bottom};
                foreach(MVISceneItem item in scene)
                {
                    if(selected.Contains(item.Id)||!item.IsSelectable||
                        item.ElementTypeId=="core.visual.connector")continue;
                    FGERect2D other=item.Bounds;
                    double[] tx={other.Left,other.Left+other.Width/2,other.Right};
                    double[] ty={other.Top,other.Top+other.Height/2,other.Bottom};
                    foreach(double target in tx)
                        foreach(double value in xs)
                        {
                            double correction=target-(value+dx);
                            double distance=Math.Abs(correction);
                            if(distance<=bestX)
                            {bestX=distance;xCorrection=correction;guideX=target;}
                        }
                    foreach(double target in ty)
                        foreach(double value in ys)
                        {
                            double correction=target-(value+dy);
                            double distance=Math.Abs(correction);
                            if(distance<=bestY)
                            {bestY=distance;yCorrection=correction;guideY=target;}
                        }
                }
            }
            dx+=xCorrection;dy+=yCorrection;
            if(snapGrid)
            {
                // 按选区外框统一吸附；不会在旋转Group内分别取整局部坐标。
                if(!guideX.HasValue)dx=Math.Round((bounds.Left+dx)/25)*25-bounds.Left;
                if(!guideY.HasValue)dy=Math.Round((bounds.Top+dy)/25)*25-bounds.Top;
            }
            return new MVIVisualSnapResult(dx,dy,guideX,guideY);
        }
    }
