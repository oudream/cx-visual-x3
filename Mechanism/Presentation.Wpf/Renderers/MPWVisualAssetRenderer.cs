using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>仅从MEM工程打包资产解码；拒绝外链、脚本、DTD及不支持的SVG节点。</summary>
    public static class MPWVisualAssetRenderer
    {
        public static ImageSource DecodeBitmap(Stream input,string mediaType)
        {
            if(input==null)throw new ArgumentNullException("input");
            if(mediaType!="image/png"&&mediaType!="image/jpeg"&&
                mediaType!="image/bmp")throw new InvalidDataException("visual.asset.media_unsupported");
            var decoder=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if(decoder.Frames.Count!=1)throw new InvalidDataException("visual.asset.multiframe");
            BitmapFrame frame=decoder.Frames[0];
            if(frame.PixelWidth<1||frame.PixelHeight<1||
                (long)frame.PixelWidth*frame.PixelHeight>64L*1024*1024)
                throw new InvalidDataException("visual.asset.pixel_budget");
            frame.Freeze();return frame;
        }
        private static double Number(string text,double fallback)
        {
            if(string.IsNullOrWhiteSpace(text))return fallback;
            double value=double.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
            if(double.IsInfinity(value)||double.IsNaN(value)||Math.Abs(value)>1000000)
                throw new InvalidDataException("visual.svg.number");
            return value;
        }
        private static double Attr(XElement e,string key,double fallback=0)
        {return Number((string)e.Attribute(key),fallback);}
        private static string[] Numbers(string s)
        {return (s??"").Split(new[]{' ', ',', '\n','\r','\t'},StringSplitOptions.RemoveEmptyEntries);}
        private static Brush Fill(string value,Brush fallback)
        {
            if(string.IsNullOrEmpty(value))return fallback;
            if(value=="none")return null;
            // 只允许字面颜色；不调用BrushConverter处理url(...)或动态资源。
            if(value[0]=='#'&&(value.Length==4||value.Length==7||value.Length==9))
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            switch(value.ToLowerInvariant())
            {
                case "black":return Brushes.Black;case "white":return Brushes.White;
                case "red":return Brushes.Red;case "green":return Brushes.Green;
                case "blue":return Brushes.Blue;case "gray":return Brushes.Gray;
                case "yellow":return Brushes.Yellow;case "orange":return Brushes.Orange;
                case "transparent":return Brushes.Transparent;
                default:throw new InvalidDataException("visual.svg.color_unsupported");
            }
        }
        private static string Inherited(XElement element,string attribute)
        {
            for(XElement e=element;e!=null;e=e.Parent)
            {
                XAttribute value=e.Attribute(attribute);
                if(value!=null)return value.Value;
            }
            return null;
        }
        private static double EffectiveOpacity(XElement element)
        {
            double opacity=1;
            for(XElement e=element;e!=null;e=e.Parent)
            {
                double value=Attr(e,"opacity",1);
                if(value<0||value>1)throw new InvalidDataException("visual.svg.opacity");
                opacity*=value;
            }
            return opacity;
        }
        /// <summary>限制SVG到此渲染器真正支持的语义，不能静默忽略filter、mask、CSS等属性。</summary>
        private static bool AllowedAttribute(string element,string key)
        {
            if(key=="fill"||key=="stroke"||key=="stroke-width"||
                key=="opacity"||key=="id")return true;
            switch(element)
            {
                case "svg":return key=="viewBox"||key=="width"||key=="height";
                case "g":return false;
                case "rect":return key=="x"||key=="y"||key=="width"||
                    key=="height"||key=="rx"||key=="ry";
                case "circle":return key=="cx"||key=="cy"||key=="r";
                case "ellipse":return key=="cx"||key=="cy"||key=="rx"||key=="ry";
                case "line":return key=="x1"||key=="y1"||key=="x2"||key=="y2";
                case "polygon":case "polyline":return key=="points";
                case "path":return key=="d";
                default:return false;
            }
        }
        private static double NonNegative(double value,string label)
        {
            if(value<0||value>1000000)
                throw new InvalidDataException("visual.svg.invalid_"+label);
            return value;
        }
        private static Geometry ReadGeometry(XElement e)
        {
            string type=e.Name.LocalName;
            switch(type)
            {
                case "rect":
                    return new RectangleGeometry(new Rect(Attr(e,"x"),Attr(e,"y"),
                        NonNegative(Attr(e,"width"),"width"),
                        NonNegative(Attr(e,"height"),"height")));
                case "circle":
                    return new EllipseGeometry(new Point(Attr(e,"cx"),Attr(e,"cy")),
                        NonNegative(Attr(e,"r"),"radius"),
                        NonNegative(Attr(e,"r"),"radius"));
                case "ellipse":
                    return new EllipseGeometry(new Point(Attr(e,"cx"),Attr(e,"cy")),
                        NonNegative(Attr(e,"rx"),"radius_x"),
                        NonNegative(Attr(e,"ry"),"radius_y"));
                case "line":
                    return new LineGeometry(new Point(Attr(e,"x1"),Attr(e,"y1")),
                        new Point(Attr(e,"x2"),Attr(e,"y2")));
                case "path":
                    string d=(string)e.Attribute("d");
                    if(string.IsNullOrWhiteSpace(d)||d.Length>250000)
                        throw new InvalidDataException("visual.svg.path_budget");
                    return Geometry.Parse(d);
                case "polygon":
                case "polyline":
                    string[] parts=Numbers((string)e.Attribute("points"));
                    if(parts.Length<4||parts.Length%2!=0||parts.Length>20000)
                        throw new InvalidDataException("visual.svg.points_budget");
                    var g=new StreamGeometry();
                    using(StreamGeometryContext context=g.Open())
                    {
                        context.BeginFigure(new Point(Number(parts[0],0),Number(parts[1],0)),
                            true,type=="polygon");
                        var points=new List<Point>();
                        for(int i=2;i<parts.Length;i+=2)
                            points.Add(new Point(Number(parts[i],0),Number(parts[i+1],0)));
                        context.PolyLineTo(points,true,false);
                    }
                    return g;
                default:throw new InvalidDataException("visual.svg.element_unsupported:"+type);
            }
        }
        public static DrawingImage DecodeSvg(Stream input)
        {
            if(input==null)throw new ArgumentNullException("input");
            var settings=new XmlReaderSettings
            {
                DtdProcessing=DtdProcessing.Prohibit,
                XmlResolver=null,
                MaxCharactersFromEntities=0,
                MaxCharactersInDocument=2000000
            };
            XDocument doc;
            using(XmlReader reader=XmlReader.Create(input,settings))
                doc=XDocument.Load(reader,LoadOptions.None);
            XElement root=doc.Root;
            if(root==null||root.Name.LocalName!="svg"||
                root.Name.NamespaceName!="http://www.w3.org/2000/svg")
                throw new InvalidDataException("visual.svg.root");
            var elements=root.DescendantsAndSelf().ToList();
            if(elements.Count>2000)throw new InvalidDataException("visual.svg.node_budget");
            foreach(XElement e in elements)
            {
                string name=e.Name.LocalName;
                if(name!="svg"&&name!="g"&&name!="rect"&&name!="circle"&&
                    name!="ellipse"&&name!="line"&&name!="path"&&
                    name!="polygon"&&name!="polyline")
                    throw new InvalidDataException("visual.svg.unsafe_or_unsupported:"+name);
                if(e.Name.NamespaceName!=root.Name.NamespaceName)
                    throw new InvalidDataException("visual.svg.foreign_namespace");
                foreach(XAttribute attribute in e.Attributes())
                {
                    if(attribute.IsNamespaceDeclaration)continue;
                    string key=attribute.Name.LocalName;
                    string value=attribute.Value;
                    if(attribute.Name.NamespaceName.Length!=0||
                        !AllowedAttribute(name,key)||
                        key.StartsWith("on",StringComparison.OrdinalIgnoreCase)||
                        value.IndexOf("url(",StringComparison.OrdinalIgnoreCase)>=0||
                        value.IndexOf("@import",StringComparison.OrdinalIgnoreCase)>=0)
                        throw new InvalidDataException("visual.svg.unsafe_or_unsupported_attribute:"+key);
                }
            }
            var group=new DrawingGroup();
            // viewBox参与图像绘制边界，不能因图形没有铺满坐标系而自行裁边。
            string viewBox=(string)root.Attribute("viewBox");
            if(!string.IsNullOrWhiteSpace(viewBox))
            {
                string[] bounds=Numbers(viewBox);
                if(bounds.Length!=4)throw new InvalidDataException("visual.svg.viewbox");
                double x=Number(bounds[0],0),y=Number(bounds[1],0);
                double width=Number(bounds[2],0),height=Number(bounds[3],0);
                if(width<=0||height<=0)
                    throw new InvalidDataException("visual.svg.viewbox_size");
                group.Children.Add(new GeometryDrawing(Brushes.Transparent,null,
                    new RectangleGeometry(new Rect(x,y,width,height))));
            }
            foreach(XElement e in elements.Where(x=>x!=root&&x.Name.LocalName!="g"))
            {
                Geometry geometry=ReadGeometry(e);
                Brush fill=Fill(Inherited(e,"fill"),e.Name.LocalName=="line"?null:Brushes.Black);
                Brush stroke=Fill(Inherited(e,"stroke"),null);
                double opacity=EffectiveOpacity(e);
                double strokeWidth=NonNegative(Number(Inherited(e,"stroke-width"),1),
                    "stroke_width");
                var drawing=new GeometryDrawing(fill,
                    stroke==null?null:new Pen(stroke,strokeWidth),
                    geometry);
                var layer=new DrawingGroup{Opacity=opacity};
                layer.Children.Add(drawing);
                group.Children.Add(layer);
            }
            if(group.Children.Count==0)throw new InvalidDataException("visual.svg.no_geometry");
            group.Freeze();
            var image=new DrawingImage(group);image.Freeze();return image;
        }
    }
}
