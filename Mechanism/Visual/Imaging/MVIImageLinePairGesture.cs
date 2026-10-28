using System;
using System.Collections.Generic;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>Ls2Ls输入状态；仅保存短命草稿，不写MVI覆盖项或MEM工程状态。</summary>
    public enum MVIImageLinePairStage
    {
        FirstStart, FirstEnd, SecondStart, SecondEnd, Complete
    }

    /// <summary>
    /// GoF State在四点手势中的最小实现：由阶段决定下一个合法输入。
    /// MPW主/副视口只传原图坐标；无WPF依赖，不另建通用工具管理器。
    /// </summary>
    public sealed class MVIImageLinePairGesture
    {
        private readonly List<FGEPoint2D> _points=new List<FGEPoint2D>(4);
        public MVIImageLinePairStage Stage
        {
            get { return (MVIImageLinePairStage)_points.Count; }
        }
        public int PointCount { get { return _points.Count; } }
        public IReadOnlyList<FGEPoint2D> Points
        {
            get { return _points.ToArray(); }
        }

        /// <summary>输入下一原图点；第四个有效点返回true，随后调用方提交并Reset。</summary>
        public bool Append(FGEPoint2D point,int width,int height)
        {
            if(width<=0 || height<=0)
                throw new ArgumentOutOfRangeException("width","图像大小必须为正。");
            if(double.IsNaN(point.X) || double.IsInfinity(point.X) ||
               double.IsNaN(point.Y) || double.IsInfinity(point.Y) ||
               point.X<0 || point.Y<0 || point.X>=width || point.Y>=height)
                throw new ArgumentOutOfRangeException("point","测量点必须位于原图有效范围。");
            if(_points.Count==4)
                throw new InvalidOperationException("当前四点已经完成；请先提交或取消。");
            if((_points.Count==1 || _points.Count==3) &&
                _points[_points.Count-1].Equals(point))
                throw new ArgumentException("同一条测量线段的起点和终点不能重合。","point");
            // 不同线段可以共端点；只有同一段起终点相同才退化。
            _points.Add(point);
            return _points.Count==4;
        }

        /// <summary>切换工具、Esc、换图或提交之后清空草稿，不触发持久对象更改。</summary>
        public void Reset() { _points.Clear(); }
    }
}
