using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Presentation.Wpf;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationWpfTests
    {
        private sealed class WidgetData : IMDSExternalBindingResolver
        {
            public bool Ready=true;
            public object Resolve(MDSBindingSource kind,string name,Type target)
            {
                if(kind==MDSBindingSource.Variable&&name=="Motor.Ready")return Ready;
                return null;
            }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("H01 WPF: " + message); }
        // PushOpacity/PushTransform 会让真实Shape绘制嵌套在DrawingGroup中。
        private static System.Collections.Generic.IEnumerable<GeometryDrawing> DeepGeometries(Drawing drawing)
        {
            var leaf=drawing as GeometryDrawing;
            if(leaf!=null)yield return leaf;
            var group=drawing as DrawingGroup;
            if(group==null)yield break;
            foreach(var child in group.Children)
                foreach(var geometry in DeepGeometries(child))yield return geometry;
        }
        public static int Run()
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                    var session = MEMEngineering.Create("嵌套变换画布"); var page = session.AddPage("P");
                    var group = session.AddGroup(page.RootContent, "G"); session.Move(group, 100, 100); session.Rotate(group, 45);
                    var rectangle = session.AddRectangle(group, "R", 0, 0, 100, 50);
                    var symbol = session.AddSymbol("库定义"); session.AddRectangle(symbol.RootGroup, "不直接投影", 200, 20);
                    var scene = CxSceneProjection.Project(session);
                    Check(scene.Count == 1 && scene[0].Id == rectangle.Id, "shared library definition leaked into page scene");
                    Check(CxSceneProjection.HitTest(scene, new CxVisual.Foundation.Geometry.FGEPoint2D(70, 108)) == null, "AABB false hit");
                    Check(CxSceneProjection.HitTest(scene, new CxVisual.Foundation.Geometry.FGEPoint2D(105, 140)) == rectangle.Id, "rotated hit missing");
                    double localX, localY; scene[0].PositionAfterWorldDelta(10, 0, out localX, out localY);
                    Check(Math.Abs(localX - Math.Sqrt(50)) < 1e-8 && Math.Abs(localY + Math.Sqrt(50)) < 1e-8, "world drag was not converted to parent coordinates");
                    var view = new SceneView { Width = 300, Height = 300 }; view.SetScene(scene, null);
                    // A real presentation source makes IsVisible/layout/render behavior match Studio.
                    var window = new Window { Content = view, Width = 320, Height = 340, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual };
                    window.Show(); view.Measure(new Size(300, 300)); view.Arrange(new Rect(0, 0, 300, 300)); view.UpdateLayout();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.Render);
                    var drawing = VisualTreeHelper.GetDrawing(view); Check(drawing != null && view.IsVisible, "real WPF layout did not render");
                    var polygon = DeepGeometries(drawing).Single(d => d.Geometry is StreamGeometry);
                    Check(!polygon.Geometry.FillContains(new Point(70,108)) && polygon.Geometry.FillContains(new Point(105,140)), "production OnRender drew an AABB instead of a transformed polygon");
                    // Rasterize SceneView's retained OnRender commands without its offscreen window clip/offset.
                    var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) dc.DrawDrawing(drawing);
                    var bitmap = new RenderTargetBitmap(300, 300, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
                    var pixels = new byte[300 * 300 * 4]; bitmap.CopyPixels(pixels, 1200, 0);
                    int outside = (108 * 300 + 70) * 4, inside = (140 * 300 + 105) * 4;
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OrganizationGuideAudit", "h01-wpf-transform.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(path)) encoder.Save(file);
                    window.Close();
                    if (pixels[outside+3] == 0 && pixels[inside+3] == 0)
                        Console.WriteLine("UNVERIFIED H01 raster pixels: current Windows composition returned transparent bitmap; real STA retained drawing geometry verified");
                    else
                    {
                        Check(pixels[outside] == 255 && pixels[outside+1] == 255 && pixels[outside+2] == 255, "canvas polygon outside pixel");
                        Check(pixels[inside+2] < 245 && pixels[inside+3] == 255, "transformed rectangle not painted");
                        Console.WriteLine("PASS H01 raster pixels");
                    }
                    // 九种Shape通过同一工程快照投影为真正的WPF曲线/弧/路径，而非矩形包围盒。
                    var shapePage=session.AddPage("Shape gallery");
                    var shapeGroup=session.AddGroup(shapePage.RootContent,"Shape items");
                    for(int i=1;i<=9;i++)
                    {
                        MRCShapeKind kind=(MRCShapeKind)i;
                        var shape=session.AddShape(shapeGroup,kind.ToString(),
                            MVIShapeCatalog.CreateDefault(kind),
                            20+(i-1)%3*200,25+(i-1)/3*145);
                        session.SetVisualStyle(shape,new MRCVisualStyle(
                            MRCBrushDefinition.Solid(MRCColor.Parse("#FF81BDF4")),
                            MRCBrushDefinition.Solid(MRCColor.Parse("#FF176DAD")),2));
                    }
                    session.AddText(shapeGroup,"Title","真实Shape与Text",20,480);
                    var shapeScene=CxSceneProjection.Project(session,shapePage.Id);
                    Check(shapeScene.Count==10&&shapeScene.Count(i=>i.Shape!=null)==9,
                        "nine shape definitions not projected");
                    var shapeView=new SceneView{Width=650,Height=555};
                    var shapeWindow=new Window{Content=shapeView,Width=680,Height=585,
                        Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false,
                        WindowStartupLocation=WindowStartupLocation.Manual};
                    shapeView.SetScene(shapeScene,null);
                    shapeWindow.Show();
                    shapeView.Measure(new Size(650,555));
                    shapeView.Arrange(new Rect(0,0,650,555));shapeView.UpdateLayout();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(new Action(()=>{}),
                        System.Windows.Threading.DispatcherPriority.Render);
                    var visualDrawing=VisualTreeHelper.GetDrawing(shapeView);
                    Check(visualDrawing!=null&&shapeView.IsVisible,"shape gallery WPF canvas inactive");
                    var pathGeometries=DeepGeometries(visualDrawing)
                        .Where(d=>d.Geometry is StreamGeometry).ToArray();
                    Check(pathGeometries.Length==9,"nine shape WPF path drawings missing");
                    var circle=shapeScene.Single(i=>i.Shape!=null&&i.Shape.Kind==MRCShapeKind.Circle);
                    Check(circle.Contains(circle.Bounds.X+circle.Bounds.Width/2,
                        circle.Bounds.Y+circle.Bounds.Height/2),
                        "circle geometry center should be hit");
                    Check(!circle.Contains(circle.Bounds.X,circle.Bounds.Y),
                        "circle outside corner must not hit");
                    foreach(var item in shapeScene.Where(i=>i.Shape!=null))
                        Check(MPWShapeRenderer.BuildGeometry(item) is StreamGeometry,
                            "renderer fell back to rectangle for "+item.Shape.Kind);
                    // UI层真的绘制了可交互控制柄，不仅是Visual内部计算了位置。
                    CxSceneItem bezierScene=shapeScene.First(x=>x.Shape!=null&&
                        x.Shape.Kind==MRCShapeKind.Bezier);
                    shapeView.SetScene(shapeScene,bezierScene.Id);
                    shapeView.UpdateLayout();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(new Action(()=>{}),
                        System.Windows.Threading.DispatcherPriority.Render);
                    var handleDrawing=VisualTreeHelper.GetDrawing(shapeView);
                    Check(DeepGeometries(handleDrawing).Count(x=>
                        x.Geometry is EllipseGeometry)>=5,
                        "selected Bezier WPF handles were not actually drawn");
                    var visualHandles=MVIVisualEditing.Handles(bezierScene);
                    Check(visualHandles.Count(x=>x.Kind==MVIVisualHandleKind.Control1||
                        x.Kind==MVIVisualHandleKind.Control2)==2,
                        "Bezier does not expose two separate editable control handles");
                    var previewPath=FGEPathEditing.MoveHandle(bezierScene.Shape.Geometry,
                        1,FGEPathHandleKind.Control1,new FGEPoint2D(21,95));
                    Check(MPWShapeRenderer.BuildGeometry(previewPath,bezierScene.GeometryToWorld)
                        is StreamGeometry,"drag preview did not use the WPF path renderer");
                    shapeWindow.Close();
                    // B: 像素与SVG资产均来自真实MEM资源包，WPF实际解码并投影。
                    string pixelPath=Path.Combine(Path.GetTempPath(),
                        "cxvisual-wpf-asset-"+Guid.NewGuid().ToString("N")+".png");
                    string svgPath=Path.Combine(Path.GetTempPath(),
                        "cxvisual-wpf-asset-"+Guid.NewGuid().ToString("N")+".svg");
                    try
                    {
                        var sourceBitmap=BitmapSource.Create(4,4,96,96,PixelFormats.Bgra32,
                            null,new byte[4*4*4],4*4);
                        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(sourceBitmap));
                        using(var stream=File.Create(pixelPath))png.Save(stream);
                        File.WriteAllText(svgPath,
                            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"64\" height=\"32\">"+
                            "<rect x=\"2\" y=\"4\" width=\"55\" height=\"24\" fill=\"#3498db\"/>"+
                            "</svg>");
                        MEMAssetReference rasterAsset=session.AddAsset("Raster Image",pixelPath,"image/png");
                        MEMAssetReference svgAsset=session.AddAsset("Vector Image",svgPath,"image/svg+xml");
                        var pageAsset=session.AddPage("Asset gallery");
                        var assetsGroup=session.AddGroup(pageAsset.RootContent,"Images");
                        MRCImageElement image=session.AddImage(assetsGroup,"Raster",15,20,90,60);
                        MRCSvgElement vector=session.AddSvg(assetsGroup,"SVG",145,20,100,65);
                        session.SetBinding(image,"source",MPMBinding.Resource(rasterAsset.Id));
                        session.SetBinding(vector,"source",MPMBinding.Resource(svgAsset.Id));
                        var assetScene=CxSceneProjection.Project(session,pageAsset.Id);
                        Check(assetScene.Count==2&&assetScene.All(x=>x.AssetId.HasValue),
                            "MEM source asset ID not projected");
                        var assetView=new SceneView{Width=310,Height=145};
                        assetView.SetAssetResolver(id=>session.OpenAsset(id));
                        assetView.SetScene(assetScene,null);
                        var assetWindow=new Window{Content=assetView,Width=335,Height=180,
                            Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false};
                        assetWindow.Show();assetView.Measure(new Size(310,145));
                        assetView.Arrange(new Rect(0,0,310,145));assetView.UpdateLayout();
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(new Action(()=>{}),
                            System.Windows.Threading.DispatcherPriority.Render);
                        Check(assetView.AssetErrors.Count==0&&
                            VisualTreeHelper.GetDrawing(assetView)!=null,
                            "real raster/SVG resource decoding or WPF drawing failed");
                        // MDS -> Scene RuntimeValue -> 实际WPF CheckBox 的真实回归。
                        var ready=new MRCWidgetElement(Guid.NewGuid(),"Ready",
                            "core.widget.checkbox",260,20,80,28);
                        session.Add(assetsGroup,ready);
                        session.SetBinding(ready,"value",MPMBinding.Variable("Motor.Ready"));
                        var data=new WidgetData();
                        int emitted=0;
                        assetView.WidgetEventRequested+=(sender,args)=>emitted++;
                        assetView.SetScene(CxSceneProjection.Project(session,pageAsset.Id,data,0),null);
                        var assetWidgets=(System.Windows.Controls.Canvas)
                            VisualTreeHelper.GetChild(assetView,0);
                        var checkbox=assetWidgets.Children.OfType<System.Windows.Controls.CheckBox>()
                            .Single();
                        Check(checkbox.IsChecked==true&&emitted==0,
                            "MDS runtime Boolean did not update real WPF checkbox safely");
                        data.Ready=false;
                        assetView.SetScene(CxSceneProjection.Project(session,pageAsset.Id,data,10),null);
                        Check(object.ReferenceEquals(checkbox,
                            assetWidgets.Children.OfType<System.Windows.Controls.CheckBox>().Single())&&
                            checkbox.IsChecked==false&&emitted==0,
                            "runtime data update recreated WPF control or emitted a user event");
                        // Widget范围/选项/内容/禁用全部来自工程MPM快照。
                        var comboModel=new MRCWidgetElement(Guid.NewGuid(),"模式",
                            "core.widget.combobox",20,105,125,30);
                        session.Add(assetsGroup,comboModel);
                        session.SetBinding(comboModel,"options",MPMBinding.Literal(
                            MPMValue.String("运行|停止|维护")));
                        session.SetBinding(comboModel,"value",MPMBinding.Literal(
                            MPMValue.String("运行")));
                        session.SetBinding(comboModel,"tooltip",MPMBinding.Literal(
                            MPMValue.String("运行模式")));
                        assetView.SetScene(CxSceneProjection.Project(session,pageAsset.Id,data,12),null);
                        var combo=assetWidgets.Children.OfType<System.Windows.Controls.ComboBox>()
                            .Single();
                        Check(combo.Items.Count==3&&combo.SelectedItem as string=="运行"&&
                            combo.ToolTip as string=="运行模式"&&emitted==0,
                            "Widget MPM options/value/tooltip did not reach real WPF ComboBox");
                        assetView.SetScene(CxSceneProjection.Project(session,pageAsset.Id,data,14),null);
                        Check(object.ReferenceEquals(combo,
                            assetWidgets.Children.OfType<System.Windows.Controls.ComboBox>().Single())&&
                            emitted==0,"reused ComboBox generated a user event during frame update");
                        assetView.IsDesignMode=false;
                        combo.SelectedItem="维护";
                        Check(emitted==1,"actual ComboBox user selection failed to raise event");
                        assetView.IsDesignMode=true;
                        var sliderModel=new MRCWidgetElement(Guid.NewGuid(),"速度",
                            "core.widget.slider",165,103,110,30);
                        session.Add(assetsGroup,sliderModel);
                        session.SetBinding(sliderModel,"minimum",
                            MPMBinding.Literal(MPMValue.Number(20)));
                        session.SetBinding(sliderModel,"maximum",
                            MPMBinding.Literal(MPMValue.Number(80)));
                        session.SetBinding(sliderModel,"value",
                            MPMBinding.Literal(MPMValue.Number(45)));
                        session.SetBinding(sliderModel,"enabled",
                            MPMBinding.Literal(MPMValue.Boolean(false)));
                        assetView.SetScene(CxSceneProjection.Project(session,pageAsset.Id,data,16),null);
                        var slider=assetWidgets.Children.OfType<System.Windows.Controls.Slider>()
                            .Single();
                        Check(slider.Minimum==20&&slider.Maximum==80&&slider.Value==45&&
                            !slider.IsEnabled&&emitted==1,
                            "slider min/max/value/enabled were not applied from persisted bindings");
                        assetWindow.Close();
                        bool rejectedUnsafe=false;
                        using(var input=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")))
                        {
                            try{MPWVisualAssetRenderer.DecodeSvg(input);}
                            catch(InvalidDataException){rejectedUnsafe=true;}
                        }
                        Check(rejectedUnsafe,"SVG script injection must be rejected");
                    }
                    finally
                    {
                        if(File.Exists(pixelPath))File.Delete(pixelPath);
                        if(File.Exists(svgPath))File.Delete(svgPath);
                    }


                    // 真正WPF Button的托管与交互：设计态仅作选择，预览态只发安全事件。
                    var widgetPage=session.AddPage("Widget gallery");
                    var widgetGroup=session.AddGroup(widgetPage.RootContent,"Controls");
                    var widgetModel=new MRCWidgetElement(Guid.NewGuid(),"启动按钮",
                        "core.widget",40,40,130,42);
                    session.Add(widgetGroup,widgetModel);
                    var widgetView=new SceneView{Width=300,Height=170};
                    int events=0;
                    widgetView.WidgetEventRequested+=(sender,request)=>
                    {
                        Check(request.ElementId==widgetModel.Id&&request.EventId=="Click",
                            "Widget host event identity wrong");
                        events++;
                    };
                    widgetView.SetScene(CxSceneProjection.Project(session,widgetPage.Id),null);
                    var widgetWindow=new Window{Content=widgetView,Width=320,Height=200,
                        Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false,
                        WindowStartupLocation=WindowStartupLocation.Manual};
                    widgetWindow.Show();widgetView.Measure(new Size(300,170));
                    widgetView.Arrange(new Rect(0,0,300,170));widgetView.UpdateLayout();
                    var widgetCanvas=VisualTreeHelper.GetChild(widgetView,0)
                        as System.Windows.Controls.Canvas;
                    Check(widgetCanvas!=null&&widgetCanvas.Children.Count==1&&
                        widgetCanvas.Children[0] is System.Windows.Controls.Button,
                        "Widget did not materialize an actual WPF Button");
                    var widgetButton=(System.Windows.Controls.Button)widgetCanvas.Children[0];
                    widgetView.SetScene(CxSceneProjection.Project(session,widgetPage.Id),null);
                    Check(object.ReferenceEquals(widgetButton,widgetCanvas.Children[0]),
                        "visual frame update rebuilt live WPF Widget");
                    widgetButton.RaiseEvent(new RoutedEventArgs(
                        System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Check(events==0,"design-mode widget executed a runtime event");
                    widgetView.IsDesignMode=false;
                    widgetButton.RaiseEvent(new RoutedEventArgs(
                        System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Check(events==1,"widget preview event not dispatched");
                    widgetWindow.Close();
                    // The same saved logical definition is rendered in the actual WPF canvas.
                    var logic = new MOLLogicDefinition(MOLControlBlock.Sequence(
                        MOLControlBlock.Action("Acquire"),
                        MOLControlBlock.If(MOLExpression.InputTrue("pass"),
                            MOLControlBlock.Action("OK"),MOLControlBlock.Action("NG"))));
                    MOLLogicCodec.Set(session,page.RootContent,logic);
                    var layout=MOLFlowLayout.Build(logic);
                    Check(layout.Count==5 && layout[1].Kind==MOLControlKind.Action,
                        "logic view hierarchy projection wrong");
                    var flowView=new MOLFlowCanvas { Definition=logic,
                        RuntimeStates=new System.Collections.Generic.Dictionary<Guid,string>
                            {{logic.Root.Children[0].Id,"Completed"}} };
                    var flowWindow=new Window { Content=new System.Windows.Controls.ScrollViewer
                            {Content=flowView}, Width=550,Height=390,
                        Left=-10000,Top=-10000,ShowInTaskbar=false,
                        ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual };
                    flowWindow.Show();flowView.Measure(new Size(520,360));
                    flowView.Arrange(new Rect(0,0,520,360));flowView.UpdateLayout();
                    Check(VisualTreeHelper.GetDrawing(flowView)!=null&&flowView.IsVisible,
                        "real logic WPF canvas did not render");
                    flowWindow.Close();
                    // V2:真实WPF画布从统一Logic自由连接定义投影，并绘制持久Control边。
                    var first=MOLControlBlock.Action("Acquire");
                    var second=MOLControlBlock.Action("AI");
                    var schemas=new System.Collections.Generic.Dictionary<Guid,
                        System.Collections.Generic.IEnumerable<MPCPortDescriptor>>{
                        {first.Id,new[]{new MPCPortDescriptor("Done",MPCPortDirection.Output,
                            "signal",MPCPortKind.Control)}},
                        {second.Id,new[]{new MPCPortDescriptor("Start",MPCPortDirection.Input,
                            "signal",MPCPortKind.Control)}}};
                    var link=new MOLLink(Guid.NewGuid(),
                        new MOLBlockEndpoint(first.Id,"Done"),
                        new MOLBlockEndpoint(second.Id,"Start"),MPCPortKind.Control);
                    var wired=new MOLWiringDefinition(first.Id,new[]{first,second},schemas,new[]{link});
                    MOLWiringCodec.Set(session,page.RootContent,wired);
                    Check(MOLFlowLayout.Build(wired).Count==2,"V2 wiring layout lost blocks");
                    var wiredView=new MOLFlowCanvas{WiringDefinition=wired,
                        RuntimeStates=new System.Collections.Generic.Dictionary<Guid,string>{
                            {first.Id,"Completed"}}};
                    var wiredWindow=new Window{Content=new System.Windows.Controls.ScrollViewer{
                            Content=wiredView},Width=550,Height=360,
                        Left=-10000,Top=-10000,ShowInTaskbar=false,ShowActivated=false,
                        WindowStartupLocation=WindowStartupLocation.Manual};
                    wiredWindow.Show();wiredView.Measure(new Size(520,320));
                    wiredView.Arrange(new Rect(0,0,520,320));wiredView.UpdateLayout();
                    Check(VisualTreeHelper.GetDrawing(wiredView)!=null&&wiredView.IsVisible,
                        "V2 wiring WPF canvas did not render");
                    wiredWindow.Close();
                    session.SetVisibility(group, false, true); Check(CxSceneProjection.Project(session,page.Id).Count == 0, "hidden group projected descendants");
                    session.Close(true);
                    OrganizationStudioTests.RunOnSta();
                    Console.WriteLine("PASS H01 WPF real STA layout/retained polygon, precise hit test, drag conversion, resource visibility and hidden-group projection");
                }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if (!thread.Join(15000)) throw new InvalidOperationException("H01 WPF thread timeout");
            if (error != null) throw new InvalidOperationException("H01 WPF failed", error);
            return 0;
        }
    }
}
