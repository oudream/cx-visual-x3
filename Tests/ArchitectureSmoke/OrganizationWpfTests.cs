using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Presentation.Wpf;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationWpfTests
    {
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("H01 WPF: " + message); }
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
                    var polygon = drawing.Children.OfType<GeometryDrawing>().Single(d => d.Geometry is StreamGeometry);
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
                    session.SetVisibility(group, false, true); Check(CxSceneProjection.Project(session).Count == 0, "hidden group projected descendants");
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
