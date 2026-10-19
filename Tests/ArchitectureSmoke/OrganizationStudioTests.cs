using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationStudioTests
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("H06 Studio: "+message);}
        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;}
        private static void Pump(){Dispatcher.CurrentDispatcher.Invoke(new Action(()=>{}),DispatcherPriority.ApplicationIdle);}
        private static void Click(Window window,string label)
        {
            window.UpdateLayout();
            var buttons=Descendants(window).OfType<Button>().ToArray();
            var button=buttons.FirstOrDefault(b=>object.Equals(b.Content,label));
            if(button==null)throw new InvalidOperationException("Missing Studio button '"+label+
                "'; visible controls: "+string.Join(" | ",buttons.Select(b=>b.Content==null?"(none)":b.Content.ToString())));
            var peer=new ButtonAutomationPeer(button);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();Pump();
        }
        private static object Get(object instance,string name){return instance.GetType().GetProperty(name).GetValue(instance,null);}
        internal static void RunOnSta()
        {
            string path=Path.Combine(Path.GetTempPath(),"cx-h06-studio-"+Guid.NewGuid().ToString("N")+".cxproj");Window window=null;Application app=null;
            try
            {
                var assembly=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CxVisual.Studio.exe"));
                if(Application.Current==null){app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/CxVisual.Studio;component/Themes/Colors.xaml",UriKind.Relative)});app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/CxVisual.Studio;component/Styles/Controls.xaml",UriKind.Relative)});}
                window=(Window)Activator.CreateInstance(assembly.GetType("CxVisual.Studio.MainWindow"));window.Left=-10000;window.Top=-10000;window.ShowInTaskbar=false;window.ShowActivated=false;
                var vm=window.DataContext;vm.GetType().GetProperty("ConfirmReplace").SetValue(vm,new Func<MEMEngineeringSession,bool>(s=>true),null);window.Show();Pump();
                var session=(MEMEngineeringSession)Get(vm,"ProjectSession");var original=session.Project.Pages[0];int oldCount=original.RootContent.DescendantsAndSelf().Count();
                Click(window,"添加页面");var page=session.Project.Pages.Last();Click(window,"添加矩形");Check(page.RootContent.VisualItems.Count==1&&original.RootContent.DescendantsAndSelf().Count()==oldCount,"active page button added to wrong page");
                // 真正点击Studio九图元工具箱，并用属性入口提交工程定义。
                var nine=new[]{"直线","矩形Shape","圆形","椭圆","折线","多边形",
                    "圆弧","贝塞尔","自由路径"};
                foreach(string label in nine)Click(window,label);
                var gallery=page.RootContent.DescendantsAndSelf().OfType<MRCShapeElement>().ToArray();
                Check(gallery.Length==9&&gallery.Select(x=>x.Shape.Kind).Distinct().Count()==9,
                    "Studio palette did not create nine persisted shapes");
                var fillBox=(TextBox)window.FindName("FillColorInput");
                fillBox.Text="#FF27B478";
                Click(window,"应用填充");
                Check(MRCVisualAppearance.StyleOf(gallery[8]).Fill.Color.Equals(
                    MRCColor.Parse("#FF27B478")),"Studio style was not committed");
                Click(window,"绑定事件");
                Check(MRCVisualAppearance.BehaviorOf(gallery[8]).EventActions.Count==1,
                    "Studio event/action was not persisted");
                Click(window,"撤销");
                Check(MRCVisualAppearance.BehaviorOf(gallery[8]).EventActions.Count==0,
                    "Studio event undo did not restore prior definition");
                Click(window,"重做");
                Check(MRCVisualAppearance.BehaviorOf(gallery[8]).EventActions.Count==1,
                    "Studio event redo failed");
                Click(window,"文字图元");
                Check(page.RootContent.DescendantsAndSelf().OfType<MRCTextElement>().Any(),
                    "Studio did not create text element");
                Click(window,"按钮控件");
                Check(page.RootContent.DescendantsAndSelf().OfType<MRCWidgetElement>().Count()==1,
                    "Studio widget tool did not create engineering element");
                var sceneHost=(CxVisual.Mechanism.Presentation.Wpf.SceneView)window.FindName("SceneHost");
                var widgetHost=VisualTreeHelper.GetChild(sceneHost,0) as Canvas;
                Check(widgetHost!=null&&widgetHost.Children.OfType<Button>().Any(),
                    "Studio widget is not a true WPF Button");
                Click(window,"预览交互");
                Check(!sceneHost.IsDesignMode,"Widget preview did not enter runtime interaction mode");
                Click(window,"返回设计");
                Check(sceneHost.IsDesignMode,"Widget preview did not return to design mode");
                Click(window,"新建组件");var symbol=session.Project.Symbols.Last();var resources=(ListBox)window.FindName("EngineeringResources");resources.SelectedItem=symbol;Pump();
                Click(window,"放置实例");var first=page.RootContent.References.Last();Click(window,"放置实例");var second=page.RootContent.References.Last();
                Check(first.Id!=second.Id&&page.RootContent.References.Count==2,"resource button did not create two real instances");
                var properties=(ItemsControl)window.FindName("EngineeringProperties");window.UpdateLayout();var text=Descendants(properties).OfType<TextBox>().First();text.Text="240";BindingOperations.GetBindingExpression(text,TextBox.TextProperty).UpdateSource();Click(window,"应用");
                Check(session.ResolveParameter(second,"宽度").Value.AsNumber()==240&&session.ResolveParameter(first,"宽度").Value.AsNumber()==120,"WPF property binding changed shared defaults");
                Click(window,"撤销");Check(session.ResolveParameter(second,"宽度").Value.AsNumber()==120,"property change required multiple undo");Click(window,"重做");
                Click(window,"编辑共享定义");Click(window,"添加矩形");Check(symbol.RootGroup.Children.Count==2,"shared definition editor context missing");Click(window,"返回");
                vm.GetType().GetMethod("SaveProject").Invoke(vm,new object[]{path});Click(window,"保存工程");vm.GetType().GetMethod("OpenProject").Invoke(vm,new object[]{path,true});Pump();
                var reopened=(MEMEngineeringSession)Get(vm,"ProjectSession");Check(reopened.FindReference(second.Id)!=null&&reopened.ResolveParameter(reopened.FindReference(second.Id),"宽度").Value.AsNumber()==240,"Studio file reopen lost instance parameter");
                Check(reopened.Project.Pages.Last().RootContent.DescendantsAndSelf()
                    .OfType<MRCShapeElement>().Count()==9,
                    "Studio nine shapes lost after real Save/Open");
                Check(reopened.Project.Pages.Last().RootContent.DescendantsAndSelf()
                    .OfType<MRCWidgetElement>().Count()==1,
                    "Studio Widget lost after Save/Open");
                Check(((ComboBox)window.FindName("EngineeringPages")).Items.Count==2&&((ListBox)window.FindName("EngineeringResources")).Items.Count==3,"real WPF collections failed to refresh after reopen");
                // 先激活逻辑编排Tab，使工具箱在可视树中。
                var tabControl=Descendants(window).OfType<TabControl>().First();
                var logicTab=Descendants(window).OfType<TabItem>()
                    .First(t=>object.Equals(t.Header,"逻辑编排 · 运行观察"));
                tabControl.SelectedItem=logicTab;
                tabControl.SelectedIndex=1;
                tabControl.ApplyTemplate();
                tabControl.UpdateLayout();
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(new Action(()=>{}),DispatcherPriority.Loaded);
                Pump();
                Check(tabControl.SelectedIndex==1,"logic tab did not become active");
                // G04 工程师界面：目录搜索、自由插入草稿、撤销重做、选择端口并编译。
                Click(window,"创建自由连接示例");
                var active=(MEMPage)Get(vm,"ActivePage");
                var beforeWiring=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(beforeWiring!=null&&beforeWiring.Blocks.Count==2,
                    "Studio V2 wiring demo not persisted in current Content");
                Click(window,"搜索能力");
                var searchResults=(ComboBox)window.FindName("FlowCapabilityResults");
                Check(searchResults.Items.Count>=2,"capability catalog search not populated");
                Click(window,"添加能力");
                var draft=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(draft.Blocks.Count==3&&draft.Links.Count==1,
                    "free Step insertion unexpectedly forced a control link");
                Click(window,"验证工程编排");
                Check(((TextBlock)window.FindName("LogicInfo")).Text.Contains("MOC_WIRING_INVALID"),
                    "Studio did not show incomplete draft error");
                Click(window,"撤销编排");
                Check(MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id)).Blocks.Count==2,
                    "Studio wiring Undo failed");
                Click(window,"重做编排");
                var afterRedo=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(afterRedo.Blocks.Count==3,"Studio wiring Redo failed");
                var selectedIds=afterRedo.Blocks.Select(x=>x.Id).ToArray();
                var evType=assembly.GetType("CxVisual.Studio.MainWindow");
                var onLink=evType.GetMethod("OnWiringLinkRequested",
                    BindingFlags.NonPublic|BindingFlags.Instance);
                onLink.Invoke(window,new object[]{null,
                    new CxVisual.Mechanism.Presentation.Wpf.MOLLinkRequestedEventArgs(
                        selectedIds[1],selectedIds[2])});
                var choiceBox=(ComboBox)window.FindName("FlowPortPairs");
                Check(choiceBox.Items.Count>0,"compatible Port pairs not shown: "+((TextBlock)window.FindName("LogicInfo")).Text);
                Click(window,"确认端口连线");
                var complete=MOLWiringCodec.Get(reopened.GetSnapshot().Find(active.RootContent.Id));
                Check(complete.Links.Count==2,"UI chosen typed Port link was not committed");
                Click(window,"验证工程编排");
                Check(((TextBlock)window.FindName("LogicInfo")).Text.Contains("编译验证通过"),
                    "Studio did not validate the completed edited flow");
                Console.WriteLine("PASS H06 Studio real WPF automation: add/switch page, resource two instances, bound parameter edit, shared Undo/Redo, definition navigation, save/reopen");
            }
            finally{if(window!=null)window.Close();if(app!=null)app.Shutdown();if(File.Exists(path))File.Delete(path);if(File.Exists(path+".cx-lock"))File.Delete(path+".cx-lock");}
        }
    }
}
