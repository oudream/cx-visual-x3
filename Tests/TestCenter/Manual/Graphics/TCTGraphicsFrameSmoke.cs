using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Mechanism.Presentation.Wpf;
namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 真实STA WPF面板操作到MEM事务的测试入口。
    /// 验证按钮事件、公共画布、Undo/Redo；不冒充系统鼠标注入与GE全通过。
    /// </summary>
    internal static class TCTGraphicsFrameSmoke
    {
        public static void Run()
        {
            TCTWorkspaceRegistry registry=TCTWorkspaceRegistry.CreateDefaults(id=>new UserControl());
            var descriptor=registry.Find("manual.graphics");
            if(descriptor==null||descriptor.Factory==null)
                throw new InvalidOperationException("图元工作区没有注册真实Frame Factory");
            TCTManualGraphicsFrame frame=descriptor.Factory() as TCTManualGraphicsFrame;
            if(frame==null)throw new InvalidOperationException("图元Factory没有创建专业Frame");
            Window window=new Window{Content=frame,Width=1200,Height=780,
                ShowInTaskbar=false,WindowStyle=WindowStyle.ToolWindow};
            try
            {
                window.Show();window.UpdateLayout();
                if(frame.Canvas==null||frame.Operations.Operations.Count<20||
                    AutomationProperties.GetAutomationId(frame.Operations)!="MPW.Visual.Operations")
                    throw new InvalidOperationException("未使用公共图元操作面板及SceneView");
                Button rect=Find<Button>(frame,"MPW.Visual.Rect");
                if(rect==null)throw new InvalidOperationException("缺少真实矩形工具按钮");
                int before=frame.Revision;
                rect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.SceneCount!=1||frame.Revision<=before)
                    throw new InvalidOperationException("共用按钮点击未创建真实MEM图元");
                frame.Execute("Circle");
                if(frame.SceneCount!=2)throw new InvalidOperationException("图元投影未更新");
                frame.Execute("edit.undo");
                if(frame.SceneCount!=1)throw new InvalidOperationException("MEM Undo失败");
                frame.Execute("edit.redo");
                if(frame.SceneCount!=2)throw new InvalidOperationException("MEM Redo失败");
                frame.Inspect();
                if(!frame.StatusMessage.Contains("Revision")||
                    frame.StatusMessage.Contains("GE Passed"))
                    throw new InvalidOperationException("观察必须展示真实Revision，不伪造测试Passed");
                Console.WriteLine("PASS TCT Graphics WPF STA: shared MPW panel, SceneView, MEM and Undo/Redo");
            }
            finally{window.Close();frame.OnWorkspaceClosed();}
        }
        private static T Find<T>(DependencyObject root,string id) where T:DependencyObject
        {
            T typed=root as T;
            if(typed!=null&&AutomationProperties.GetAutomationId(typed)==id)return typed;
            int count=VisualTreeHelper.GetChildrenCount(root);
            for(int i=0;i<count;i++)
            {
                T found=Find<T>(VisualTreeHelper.GetChild(root,i),id);
                if(found!=null)return found;
            }
            return null;
        }
    }
}