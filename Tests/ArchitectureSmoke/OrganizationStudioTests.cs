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
            window.UpdateLayout();var button=Descendants(window).OfType<Button>().First(b=>object.Equals(b.Content,label));
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
                Click(window,"新建组件");var symbol=session.Project.Symbols.Last();var resources=(ListBox)window.FindName("EngineeringResources");resources.SelectedItem=symbol;Pump();
                Click(window,"放置实例");var first=page.RootContent.References.Last();Click(window,"放置实例");var second=page.RootContent.References.Last();
                Check(first.Id!=second.Id&&page.RootContent.References.Count==2,"resource button did not create two real instances");
                var properties=(ItemsControl)window.FindName("EngineeringProperties");window.UpdateLayout();var text=Descendants(properties).OfType<TextBox>().First();text.Text="240";BindingOperations.GetBindingExpression(text,TextBox.TextProperty).UpdateSource();Click(window,"应用");
                Check(session.ResolveParameter(second,"宽度").Value.AsNumber()==240&&session.ResolveParameter(first,"宽度").Value.AsNumber()==120,"WPF property binding changed shared defaults");
                Click(window,"撤销");Check(session.ResolveParameter(second,"宽度").Value.AsNumber()==120,"property change required multiple undo");Click(window,"重做");
                Click(window,"编辑共享定义");Click(window,"添加矩形");Check(symbol.RootGroup.Children.Count==2,"shared definition editor context missing");Click(window,"返回");
                vm.GetType().GetMethod("SaveProject").Invoke(vm,new object[]{path});Click(window,"保存工程");vm.GetType().GetMethod("OpenProject").Invoke(vm,new object[]{path,true});Pump();
                var reopened=(MEMEngineeringSession)Get(vm,"ProjectSession");Check(reopened.FindReference(second.Id)!=null&&reopened.ResolveParameter(reopened.FindReference(second.Id),"宽度").Value.AsNumber()==240,"Studio file reopen lost instance parameter");
                Check(((ComboBox)window.FindName("EngineeringPages")).Items.Count==2&&((ListBox)window.FindName("EngineeringResources")).Items.Count==3,"real WPF collections failed to refresh after reopen");
                Console.WriteLine("PASS H06 Studio real WPF automation: add/switch page, resource two instances, bound parameter edit, shared Undo/Redo, definition navigation, save/reopen");
            }
            finally{if(window!=null)window.Close();if(app!=null)app.Shutdown();if(File.Exists(path))File.Delete(path);if(File.Exists(path+".cx-lock"))File.Delete(path+".cx-lock");}
        }
    }
}
