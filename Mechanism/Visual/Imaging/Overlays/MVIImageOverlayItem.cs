using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 原图坐标系中的 ROI、标注或测量覆盖项。与旧ImageOverlayItem一致保持Kind/Layer/
    /// Bounds/Points/Style等语义，但使用现有FGE几何并以不可变快照更新，避免异步渲染脏状态。
    /// 属于图像会话覆盖数据，不是另一个MEM工程/Undo真源。
    /// </summary>
    /// <summary>ROI八方向编辑柄，仅描述原图边界几何，不依赖WPF或任何厂商SDK。</summary>
    public enum MVIImageResizeHandle
    {
        TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left
    }

    public sealed class MVIImageOverlayItem
    {
        private readonly FGEPoint2D[] _points;
        private readonly IReadOnlyList<FGEPoint2D> _readonlyPoints;

        public MVIImageOverlayItem(MVIImageOverlayKind kind, MVIImageOverlayLayer layer,
            FGERect2D bounds, IEnumerable<FGEPoint2D> points = null,
            string text = null, string externalId = null,
            int argb = unchecked((int)0xFFFF4040), double lineWidth = 1.5,
            Guid? id = null, bool isVisible = true, bool isLocked = false)
        {
            if (!Enum.IsDefined(typeof(MVIImageOverlayKind), kind))
                throw new ArgumentOutOfRangeException("kind");
            if (!Enum.IsDefined(typeof(MVIImageOverlayLayer), layer))
                throw new ArgumentOutOfRangeException("layer");
            if (double.IsNaN(lineWidth) || double.IsInfinity(lineWidth) || lineWidth <= 0 || lineWidth > 100)
                throw new ArgumentOutOfRangeException("lineWidth");
            _points = points == null ? new FGEPoint2D[0] : points.ToArray();
            if (_points.Length > 100000) throw new ArgumentException("叠加几何点数超限。", "points");
            if (kind == MVIImageOverlayKind.RotatedRectangleRoi)
            {
                if (layer != MVIImageOverlayLayer.Roi || _points.Length != 4)
                    throw new ArgumentException("旋转矩形必须拥有四个原图角点并属于ROI层。","points");
                FGEVector2D u=_points[1]-_points[0];
                FGEVector2D v=_points[2]-_points[1];
                FGEVector2D oppositeU=_points[2]-_points[3];
                FGEVector2D oppositeV=_points[3]-_points[0];
                double tolerance=1e-7*Math.Max(1,u.Length*v.Length);
                if(u.Length<0.25 || v.Length<0.25 ||
                    Math.Abs(FGEVector2D.Dot(u,v))>tolerance ||
                    (oppositeU-u).Length>1e-7*Math.Max(1,u.Length) ||
                    (oppositeV-v).Length>1e-7*Math.Max(1,v.Length))
                    throw new ArgumentException("旋转矩形四角必须构成正交、非退化矩形。","points");
                double l=_points.Min(p=>p.X),r=_points.Max(p=>p.X);
                double t=_points.Min(p=>p.Y),b=_points.Max(p=>p.Y);
                if(Math.Abs(bounds.Left-l)>1e-7 ||
                    Math.Abs(bounds.Top-t)>1e-7 ||
                    Math.Abs(bounds.Right-r)>1e-7 ||
                    Math.Abs(bounds.Bottom-b)>1e-7)
                    throw new ArgumentException("旋转矩形Bounds必须匹配四角外接框。","bounds");
            }
            if ((kind == MVIImageOverlayKind.PolygonRoi || kind == MVIImageOverlayKind.PolygonAnnotation)
                && _points.Length < 3)
                throw new ArgumentException("多边形至少需要三个点。", "points");
            if ((kind == MVIImageOverlayKind.DistanceMeasurement ||
                 kind == MVIImageOverlayKind.HorizontalMeasurement ||
                 kind == MVIImageOverlayKind.VerticalMeasurement ||
                 kind == MVIImageOverlayKind.LineProfileMeasurement ||
                 kind == MVIImageOverlayKind.ArrowAnnotation) && _points.Length < 2)
                throw new ArgumentException("线段测量或箭头至少需要两个点。", "points");
            if (kind == MVIImageOverlayKind.AngleMeasurement && _points.Length < 3)
                throw new ArgumentException("角度测量至少需要三个点。", "points");
            if (kind == MVIImageOverlayKind.LinePairMeasurement &&
                (layer != MVIImageOverlayLayer.Measurement || _points.Length != 4))
                throw new ArgumentException("两线段测量必须在测量层且恰有四个原图点。","points");
            Id = id ?? Guid.NewGuid();
            if (Id == Guid.Empty) throw new ArgumentException("叠加项ID不能为Empty。", "id");
            Kind = kind; Layer = layer; Bounds = bounds;
            _readonlyPoints = Array.AsReadOnly(_points);
            Text = text ?? string.Empty; ExternalId = externalId ?? string.Empty;
            Argb = argb; LineWidth = lineWidth; IsVisible = isVisible; IsLocked = isLocked;
        }

        public Guid Id { get; private set; }
        public MVIImageOverlayKind Kind { get; private set; }
        public MVIImageOverlayLayer Layer { get; private set; }
        /// <summary>连续空间外框，和FIMPixelRegion闭区间像素统计ROI必须显式转换。</summary>
        public FGERect2D Bounds { get; private set; }
        public IReadOnlyList<FGEPoint2D> Points { get { return _readonlyPoints; } }
        /// <summary>当前对象是否是真实可统计面积的ROI；测量线和缺陷标记不是ROI。</summary>
        public bool IsAreaRoi
        {
            get
            {
                return Layer==MVIImageOverlayLayer.Roi &&
                    (Kind==MVIImageOverlayKind.RectangleRoi ||
                     Kind==MVIImageOverlayKind.CircleRoi ||
                     Kind==MVIImageOverlayKind.EllipseRoi ||
                     Kind==MVIImageOverlayKind.PolygonRoi ||
                     Kind==MVIImageOverlayKind.RotatedRectangleRoi);
            }
        }

        /// <summary>
        /// 对原图像素中心进行形状精确命中；供FIM掩膜回调复用，不以Bounds冒充圆/多边形。
        /// </summary>
        public bool ContainsRoiPixelCenter(int pixelX,int pixelY)
        {
            if(!IsAreaRoi)return false;
            double x=pixelX+0.5,y=pixelY+0.5;
            if(x<Bounds.Left || x>Bounds.Right || y<Bounds.Top || y>Bounds.Bottom)
                return false;
            if(Kind==MVIImageOverlayKind.PolygonRoi ||
                Kind==MVIImageOverlayKind.RotatedRectangleRoi)
                return FGEGeometry2D.PointInPolygon(new FGEPoint2D(x,y),_points);
            if(Kind==MVIImageOverlayKind.CircleRoi ||
                Kind==MVIImageOverlayKind.EllipseRoi)
            {
                double rx=Bounds.Width/2,ry=Bounds.Height/2;
                if(rx<=0 || ry<=0)return false;
                double nx=(x-(Bounds.Left+rx))/rx;
                double ny=(y-(Bounds.Top+ry))/ry;
                return nx*nx+ny*ny<=1.0+1e-12;
            }
            return true;
        }

        public string Text { get; private set; }
        public string ExternalId { get; private set; }
        public int Argb { get; private set; }
        public double LineWidth { get; private set; }
        public bool IsVisible { get; private set; }
        public bool IsLocked { get; private set; }

        /// <summary>旋转矩形以原图X右/Y下坐标的顺时针正角表示。</summary>
        public double RotatedRectangleAngleDegrees
        {
            get
            {
                RequireRotatedRectangle();
                return Math.Atan2(_points[1].Y-_points[0].Y,
                    _points[1].X-_points[0].X)*180.0/Math.PI;
            }
        }
        public double RotatedRectangleWidth
        {
            get { RequireRotatedRectangle(); return _points[0].DistanceTo(_points[1]); }
        }
        public double RotatedRectangleHeight
        {
            get { RequireRotatedRectangle(); return _points[1].DistanceTo(_points[2]); }
        }
        public double RotatedRectangleArea
        {
            get { return RotatedRectangleWidth*RotatedRectangleHeight; }
        }
        private void RequireRotatedRectangle()
        {
            if(Kind!=MVIImageOverlayKind.RotatedRectangleRoi)
                throw new InvalidOperationException("当前对象不是旋转矩形ROI。");
        }

        public static MVIImageOverlayItem CreateRotatedRectangle(FGERect2D rect,
            double angleDegrees,int imageWidth,int imageHeight)
        {
            return CreateRotatedGeometry(rect.X+rect.Width/2.0,rect.Y+rect.Height/2.0,
                rect.Width,rect.Height,angleDegrees,imageWidth,imageHeight,null);
        }
        public MVIImageOverlayItem WithRotatedRectangleAngle(double angleDegrees,
            int imageWidth,int imageHeight)
        {
            RequireRotatedRectangle();
            if(IsLocked) throw new InvalidOperationException("旋转矩形已锁定。");
            return CreateRotatedGeometry((_points[0].X+_points[2].X)/2.0,
                (_points[0].Y+_points[2].Y)/2.0,RotatedRectangleWidth,
                RotatedRectangleHeight,angleDegrees,imageWidth,imageHeight,this);
        }
        private static MVIImageOverlayItem CreateRotatedGeometry(double cx,double cy,
            double width,double height,double angle,int imageWidth,int imageHeight,
            MVIImageOverlayItem original)
        {
            FGEValidation.Finite(cx,"cx");FGEValidation.Finite(cy,"cy");
            FGEValidation.Finite(width,"width");FGEValidation.Finite(height,"height");
            FGEValidation.Finite(angle,"angle");
            if(width<0.25 || height<0.25)
                throw new ArgumentOutOfRangeException("width","旋转矩形宽高不能小于0.25原图像素。");
            if(imageWidth<=0 || imageHeight<=0)
                throw new ArgumentOutOfRangeException("imageWidth");
            double radians=(angle%360.0)*Math.PI/180.0;
            double cos=Math.Cos(radians),sin=Math.Sin(radians);
            double halfWidth=width/2.0,halfHeight=height/2.0;
            double[] localX={-halfWidth,halfWidth,halfWidth,-halfWidth};
            double[] localY={-halfHeight,-halfHeight,halfHeight,halfHeight};
            FGEPoint2D[] corners=new FGEPoint2D[4];
            for(int i=0;i<4;i++)
            {
                double x=cx+localX[i]*cos-localY[i]*sin;
                double y=cy+localX[i]*sin+localY[i]*cos;
                if(x < -1e-8 || y < -1e-8 ||
                    x > imageWidth+1e-8 || y > imageHeight+1e-8)
                    throw new ArgumentOutOfRangeException("angle",
                        "旋转结果有角点超出原图，不修改原对象。");
                corners[i]=new FGEPoint2D(x,y);
            }
            double left=corners.Min(p=>p.X),right=corners.Max(p=>p.X);
            double top=corners.Min(p=>p.Y),bottom=corners.Max(p=>p.Y);
            return new MVIImageOverlayItem(MVIImageOverlayKind.RotatedRectangleRoi,
                MVIImageOverlayLayer.Roi,new FGERect2D(left,top,right-left,bottom-top),
                corners,original==null?null:original.Text,
                original==null?null:original.ExternalId,
                original==null?unchecked((int)0xffff4040):original.Argb,
                original==null?1.5:original.LineWidth,
                original==null?(Guid?)null:original.Id,
                original==null || original.IsVisible,
                original!=null && original.IsLocked);
        }

        /// <summary>
        /// 旋转矩形沿自身局部X/Y轴缩放；固定对侧角/边，
        /// 全程保持四个正交顶点与图像有效域，不使用轴对齐Bounds拉伸。
        /// </summary>
        public MVIImageOverlayItem WithResizedRotatedHandle(MVIImageResizeHandle handle,
            FGEPoint2D point,int imageWidth,int imageHeight)
        {
            RequireRotatedRectangle();
            if(IsLocked) throw new InvalidOperationException("锁定的旋转矩形不能改变尺寸。");
            if(!Enum.IsDefined(typeof(MVIImageResizeHandle),handle))
                throw new ArgumentOutOfRangeException("handle");
            if(imageWidth<=0 || imageHeight<=0)
                throw new ArgumentOutOfRangeException("imageWidth");
            double width=RotatedRectangleWidth,height=RotatedRectangleHeight;
            FGEPoint2D origin=_points[0];
            double ux=(_points[1].X-origin.X)/width,uy=(_points[1].Y-origin.Y)/width;
            double vx=(_points[3].X-origin.X)/height,vy=(_points[3].Y-origin.Y)/height;
            double dx=FGEValidation.Finite(point.X,"point")-origin.X;
            double dy=FGEValidation.Finite(point.Y,"point")-origin.Y;
            double x=dx*ux+dy*uy,y=dx*vx+dy*vy;
            double left=0,right=width,top=0,bottom=height;
            const double min=0.25;
            switch(handle)
            {
                case MVIImageResizeHandle.TopLeft:
                case MVIImageResizeHandle.Left:
                case MVIImageResizeHandle.BottomLeft: left=Math.Min(right-min,x);break;
                case MVIImageResizeHandle.TopRight:
                case MVIImageResizeHandle.Right:
                case MVIImageResizeHandle.BottomRight: right=Math.Max(left+min,x);break;
            }
            switch(handle)
            {
                case MVIImageResizeHandle.TopLeft:
                case MVIImageResizeHandle.Top:
                case MVIImageResizeHandle.TopRight: top=Math.Min(bottom-min,y);break;
                case MVIImageResizeHandle.BottomLeft:
                case MVIImageResizeHandle.Bottom:
                case MVIImageResizeHandle.BottomRight: bottom=Math.Max(top+min,y);break;
            }
            double[] localX={left,right,right,left};
            double[] localY={top,top,bottom,bottom};
            FGEPoint2D[] corners=new FGEPoint2D[4];
            for(int i=0;i<4;i++)
            {
                double px=origin.X+localX[i]*ux+localY[i]*vx;
                double py=origin.Y+localX[i]*uy+localY[i]*vy;
                if(px< -1e-8 || py< -1e-8 ||
                    px>imageWidth+1e-8 || py>imageHeight+1e-8)
                    throw new ArgumentOutOfRangeException("point",
                        "调整后的旋转矩形角点超出原图。");
                corners[i]=new FGEPoint2D(px,py);
            }
            double l=corners.Min(p=>p.X),t=corners.Min(p=>p.Y);
            double r=corners.Max(p=>p.X),b=corners.Max(p=>p.Y);
            return new MVIImageOverlayItem(Kind,Layer,
                new FGERect2D(l,t,r-l,b-t),corners,Text,ExternalId,
                Argb,LineWidth,Id,IsVisible,IsLocked);
        }

        /// <summary>按原图坐标缩放边界与顶点，不能只改Bounds而令测量线/多边形顶点停在原位。</summary>
        public MVIImageOverlayItem WithBounds(FGERect2D bounds)
        {
            if (IsLocked) throw new InvalidOperationException("ROI已锁定，禁止缩放。");
            if(Kind==MVIImageOverlayKind.RotatedRectangleRoi)
                throw new InvalidOperationException("旋转矩形不能以外接Bounds进行非等比变换。");
            double scaleX = Bounds.Width <= 0 ? 1 : bounds.Width / Bounds.Width;
            double scaleY = Bounds.Height <= 0 ? 1 : bounds.Height / Bounds.Height;
            FGEPoint2D[] points = _points.Select(p => new FGEPoint2D(
                bounds.X + (p.X - Bounds.X) * scaleX,
                bounds.Y + (p.Y - Bounds.Y) * scaleY)).ToArray();
            return new MVIImageOverlayItem(Kind, Layer, bounds, points, Text, ExternalId,
                Argb, LineWidth, Id, IsVisible, IsLocked);
        }
        /// <summary>
        /// 基于拖动开始时的不可变快照编辑ROI边界，保持Guid、图层、样式和多边形顶点对应。
        /// 锚定对侧边/角，不允许倒置或超出原图；最小尺寸为1/4原始像素。
        /// </summary>
        public MVIImageOverlayItem WithResizedRoiHandle(MVIImageResizeHandle handle,
            FGEPoint2D position, int imageWidth, int imageHeight)
        {
            if (IsLocked) throw new InvalidOperationException("ROI已锁定，不能拖动编辑柄。");
            if (Layer != MVIImageOverlayLayer.Roi ||
                (Kind != MVIImageOverlayKind.RectangleRoi &&
                 Kind != MVIImageOverlayKind.CircleRoi &&
                 Kind != MVIImageOverlayKind.EllipseRoi &&
                 Kind != MVIImageOverlayKind.PolygonRoi))
                throw new InvalidOperationException("仅几何ROI支持当前八方向缩放；标注及测量不应被悄悄修改。");
            if (!Enum.IsDefined(typeof(MVIImageResizeHandle), handle))
                throw new ArgumentOutOfRangeException("handle");
            if (imageWidth <= 0 || imageHeight <= 0)
                throw new ArgumentOutOfRangeException("imageWidth");
            if (Bounds.Width <= 0 || Bounds.Height <= 0 || Bounds.Left < 0 || Bounds.Top < 0 ||
                Bounds.Right > imageWidth || Bounds.Bottom > imageHeight)
                throw new InvalidOperationException("原ROI边界不合法，不能编辑。");
            double x = Math.Max(0, Math.Min(imageWidth,FGEValidation.Finite(position.X,"position")));
            double y = Math.Max(0, Math.Min(imageHeight,FGEValidation.Finite(position.Y,"position")));
            double left = Bounds.Left, right = Bounds.Right;
            double top = Bounds.Top, bottom = Bounds.Bottom;
            const double minimum = 0.25;
            switch (handle)
            {
                case MVIImageResizeHandle.TopLeft:
                case MVIImageResizeHandle.Left:
                case MVIImageResizeHandle.BottomLeft:
                    left = Math.Min(right - minimum,x); break;
                case MVIImageResizeHandle.TopRight:
                case MVIImageResizeHandle.Right:
                case MVIImageResizeHandle.BottomRight:
                    right = Math.Max(left + minimum,x); break;
            }
            switch (handle)
            {
                case MVIImageResizeHandle.TopLeft:
                case MVIImageResizeHandle.Top:
                case MVIImageResizeHandle.TopRight:
                    top = Math.Min(bottom - minimum,y); break;
                case MVIImageResizeHandle.BottomLeft:
                case MVIImageResizeHandle.Bottom:
                case MVIImageResizeHandle.BottomRight:
                    bottom = Math.Max(top + minimum,y); break;
            }
            return WithBounds(new FGERect2D(left,top,right-left,bottom-top));
        }

        /// <summary>
        /// 编辑单个多边形顶点；始终以原图坐标为准，并重新计算外接矩形。
        /// 不修改其它顶点、Guid、样式及锁；拒绝退化、自交或绕向翻转。
        /// </summary>
        public MVIImageOverlayItem WithMovedPolygonVertex(int index,
            FGEPoint2D position, int imageWidth, int imageHeight)
        {
            if (IsLocked) throw new InvalidOperationException("多边形已锁定，不能编辑顶点。");
            if (Kind != MVIImageOverlayKind.PolygonRoi || Layer != MVIImageOverlayLayer.Roi)
                throw new InvalidOperationException("仅多边形ROI支持逐顶点编辑。");
            if (index < 0 || index >= _points.Length)
                throw new ArgumentOutOfRangeException("index");
            if (imageWidth <= 0 || imageHeight <= 0)
                throw new ArgumentOutOfRangeException("imageWidth");
            double x = Math.Max(0, Math.Min(imageWidth, FGEValidation.Finite(position.X,"position")));
            double y = Math.Max(0, Math.Min(imageHeight, FGEValidation.Finite(position.Y,"position")));
            FGEPoint2D[] points = (FGEPoint2D[])_points.Clone();
            points[index] = new FGEPoint2D(x,y);
            double oldArea = PolygonSignedDoubleArea(_points);
            double newArea = PolygonSignedDoubleArea(points);
            if (Math.Abs(newArea) < 1e-8 || Math.Abs(oldArea) < 1e-8 ||
                Math.Sign(newArea) != Math.Sign(oldArea))
                throw new ArgumentException("多边形退化或编辑导致绕向翻转。","position");
            int count=points.Length;
            for (int i=0;i<count;i++)
            {
                if ((points[(i+1)%count]-points[i]).LengthSquared < 1e-12)
                    throw new ArgumentException("相邻多边形顶点不能重合。","position");
            }
            // 单顶点编辑只改变其左右两条边。其余边与边的拓扑不变，
            // 不必对全部边进行 O(n²) 两两求交；鼠标拖动须维持 O(n)。
            int previous=(index+count-1)%count;
            int[] changedEdges = {previous,index};
            foreach (int changedEdge in changedEdges)
            {
                FGEPoint2D a=points[changedEdge];
                FGEPoint2D b=points[(changedEdge+1)%count];
                for (int edge=0;edge<count;edge++)
                {
                    if (edge==changedEdge ||
                        edge==(changedEdge+1)%count ||
                        edge==(changedEdge+count-1)%count) continue;
                    if (PolygonSegmentsIntersect(a,b,
                        points[edge],points[(edge+1)%count]))
                        throw new ArgumentException("编辑将产生自交多边形。","position");
                }
            }
            double left=points.Min(p=>p.X),right=points.Max(p=>p.X);
            double top=points.Min(p=>p.Y),bottom=points.Max(p=>p.Y);
            return new MVIImageOverlayItem(Kind,Layer,
                new FGERect2D(left,top,right-left,bottom-top),points,Text,ExternalId,
                Argb,LineWidth,Id,IsVisible,IsLocked);
        }

        /// <summary>在多边形第edgeIndex条边后插入原图顶点，不改变其它顶点及Guid。</summary>
        public MVIImageOverlayItem WithInsertedPolygonVertex(int edgeIndex,
            FGEPoint2D position,int imageWidth,int imageHeight)
        {
            RequireEditablePolygon();
            if(edgeIndex<0 || edgeIndex>=_points.Length)
                throw new ArgumentOutOfRangeException("edgeIndex");
            if(_points.Length>=100000)
                throw new InvalidOperationException("多边形顶点数已达到上限。");
            if(imageWidth<=0 || imageHeight<=0)
                throw new ArgumentOutOfRangeException("imageWidth");
            if(position.X<0 || position.Y<0 ||
                position.X>imageWidth || position.Y>imageHeight)
                throw new ArgumentOutOfRangeException("position","插入顶点超出原图范围。");
            FGEPoint2D[] updated=new FGEPoint2D[_points.Length+1];
            Array.Copy(_points,0,updated,0,edgeIndex+1);
            updated[edgeIndex+1]=position;
            Array.Copy(_points,edgeIndex+1,updated,edgeIndex+2,
                _points.Length-edgeIndex-1);
            return WithValidatedPolygonTopology(updated,
                new []{edgeIndex,edgeIndex+1},"position");
        }

        /// <summary>删除单个多边形顶点；至少保留三个有效且不自交的顶点。</summary>
        public MVIImageOverlayItem WithRemovedPolygonVertex(int vertexIndex)
        {
            RequireEditablePolygon();
            if(vertexIndex<0 || vertexIndex>=_points.Length)
                throw new ArgumentOutOfRangeException("vertexIndex");
            if(_points.Length<=3)
                throw new InvalidOperationException("多边形最少保留三个顶点。");
            FGEPoint2D[] updated=new FGEPoint2D[_points.Length-1];
            Array.Copy(_points,0,updated,0,vertexIndex);
            Array.Copy(_points,vertexIndex+1,updated,vertexIndex,
                _points.Length-vertexIndex-1);
            // 删除顶点后，只改变原顶点两侧拼接而成的一条边。
            int affected=(vertexIndex+updated.Length-1)%updated.Length;
            return WithValidatedPolygonTopology(updated,new []{affected},"vertexIndex");
        }

        private void RequireEditablePolygon()
        {
            if(IsLocked) throw new InvalidOperationException("多边形已锁定，不能修改顶点。");
            if(Kind!=MVIImageOverlayKind.PolygonRoi || Layer!=MVIImageOverlayLayer.Roi)
                throw new InvalidOperationException("仅多边形ROI可进行顶点编辑。");
        }

        private MVIImageOverlayItem WithValidatedPolygonTopology(FGEPoint2D[] points,
            int[] affectedEdges,string parameter)
        {
            double before=PolygonSignedDoubleArea(_points);
            double after=PolygonSignedDoubleArea(points);
            if(Math.Abs(before)<1e-8 || Math.Abs(after)<1e-8 ||
                Math.Sign(before)!=Math.Sign(after))
                throw new ArgumentException("多边形退化或绕向翻转。",parameter);
            int n=points.Length;
            for(int i=0;i<n;i++)
                if((points[(i+1)%n]-points[i]).LengthSquared<1e-12)
                    throw new ArgumentException("多边形边长过短或顶点重合。",parameter);
            // 插入/删除只改变一或两条边，仅与其余非邻接边相交判定，复杂度O(n)。
            foreach(int changed in affectedEdges)
            {
                FGEPoint2D a=points[changed],b=points[(changed+1)%n];
                for(int i=0;i<n;i++)
                {
                    if(i==changed || i==(changed+1)%n ||
                        i==(changed+n-1)%n) continue;
                    if(PolygonSegmentsIntersect(a,b,points[i],points[(i+1)%n]))
                        throw new ArgumentException("顶点编辑将产生自交多边形。",parameter);
                }
            }
            double left=points.Min(p=>p.X),right=points.Max(p=>p.X);
            double top=points.Min(p=>p.Y),bottom=points.Max(p=>p.Y);
            return new MVIImageOverlayItem(Kind,Layer,
                new FGERect2D(left,top,right-left,bottom-top),
                points,Text,ExternalId,Argb,LineWidth,Id,IsVisible,IsLocked);
        }

        private static double PolygonSignedDoubleArea(IReadOnlyList<FGEPoint2D> points)
        {
            double total=0;
            for (int i=0;i<points.Count;i++)
            {
                FGEPoint2D a=points[i],b=points[(i+1)%points.Count];
                total+=a.X*b.Y-b.X*a.Y;
            }
            return total;
        }

        private static double PolygonCross(FGEPoint2D a,FGEPoint2D b,FGEPoint2D p)
        { return (b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X); }

        private static bool PolygonOnSegment(FGEPoint2D p,FGEPoint2D a,FGEPoint2D b)
        {
            return p.X>=Math.Min(a.X,b.X)-1e-10 && p.X<=Math.Max(a.X,b.X)+1e-10 &&
                p.Y>=Math.Min(a.Y,b.Y)-1e-10 && p.Y<=Math.Max(a.Y,b.Y)+1e-10;
        }

        private static bool PolygonSegmentsIntersect(FGEPoint2D a,FGEPoint2D b,
            FGEPoint2D c,FGEPoint2D d)
        {
            double abC=PolygonCross(a,b,c),abD=PolygonCross(a,b,d);
            double cdA=PolygonCross(c,d,a),cdB=PolygonCross(c,d,b);
            if (((abC>1e-10 && abD< -1e-10) || (abC< -1e-10 && abD>1e-10)) &&
                ((cdA>1e-10 && cdB< -1e-10) || (cdA< -1e-10 && cdB>1e-10))) return true;
            return (Math.Abs(abC)<=1e-10 && PolygonOnSegment(c,a,b)) ||
                (Math.Abs(abD)<=1e-10 && PolygonOnSegment(d,a,b)) ||
                (Math.Abs(cdA)<=1e-10 && PolygonOnSegment(a,c,d)) ||
                (Math.Abs(cdB)<=1e-10 && PolygonOnSegment(b,c,d));
        }

        public MVIImageOverlayItem WithVisibility(bool visible)
        {
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, Text, ExternalId,
                Argb, LineWidth, Id, visible, IsLocked);
        }
        public MVIImageOverlayItem WithLocked(bool locked)
        {
            return new MVIImageOverlayItem(Kind,Layer,Bounds,_points,Text,ExternalId,
                Argb,LineWidth,Id,IsVisible,locked);
        }
        public MVIImageOverlayItem WithText(string text)
        {
            if (IsLocked) throw new InvalidOperationException("叠加对象已锁定，不能修改文字。");
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, text, ExternalId,
                Argb, LineWidth, Id, IsVisible, IsLocked);
        }
        public MVIImageOverlayItem WithStyle(int argb, double lineWidth)
        {
            if (IsLocked) throw new InvalidOperationException("叠加对象已锁定，不能修改样式。");
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, Text, ExternalId,
                argb, lineWidth, Id, IsVisible, IsLocked);
        }
        public MVIImageOverlayItem Translate(double dx, double dy)
        {
            if (IsLocked) throw new InvalidOperationException("叠加对象已锁定，不能移动。");
            FGEValidation.Finite(dx, "dx"); FGEValidation.Finite(dy, "dy");
            FGERect2D rect = new FGERect2D(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height);
            FGEPoint2D[] translated = _points.Select(p => new FGEPoint2D(p.X + dx, p.Y + dy)).ToArray();
            return new MVIImageOverlayItem(Kind, Layer, rect, translated, Text, ExternalId,
                Argb, LineWidth, Id, IsVisible, IsLocked);
        }
    }
}
