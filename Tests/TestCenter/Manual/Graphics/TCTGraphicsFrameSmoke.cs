using System;
using System.IO;
using System.Linq;
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
            string file=Path.Combine(Path.GetTempPath(),"cx-graphics-"+Guid.NewGuid().ToString("N")+".cxproj");
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
                // 工程对象树与画布选择必须是同一身份，而非右侧静态字符串。
                Guid[] ids=frame.SceneIds.ToArray();
                ListBox objects=Find<ListBox>(frame,"TCT.Graphics.Objects");
                if(objects==null||objects.Items.Count!=2)
                    throw new InvalidOperationException("图元对象树未绑定真实Scene");
                objects.SelectedItem=objects.Items.Cast<object>()
                    .First(item=>((CxVisual.Mechanism.Visual.MVISceneItem)item).Id==ids[0]);
                if(frame.SelectedId!=ids[0]||
                    !frame.Canvas.SelectedVisualIds.Contains(ids[0]))
                    throw new InvalidOperationException("对象树选择未同步画布与MVI");
                // 点击真实属性按钮，检查Local几何被MEM事务保存且能Undo。
                TextBox x=Find<TextBox>(frame,"TCT.Graphics.Geometry.X");
                Button apply=Find<Button>(frame,"TCT.Graphics.ApplyGeometry");
                if(x==null||apply==null||!x.IsEnabled)
                    throw new InvalidOperationException("已选中图元却没有可操作的几何属性");
                int beforeEdit=frame.Revision;
                x.Text="87";
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Revision<=beforeEdit)
                    throw new InvalidOperationException("属性输入没有走真实MVI/MEM事务");
                int afterEdit=frame.Revision;
                x.Text="NaN";
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Revision!=afterEdit||!frame.StatusMessage.Contains("被拒绝"))
                    throw new InvalidOperationException("非法数字改变了工程或未报告拒绝");
                // 图元组操作与Studio共用Core Group/Ungroup事务，不创建新的树或历史。
                frame.SelectObjects(ids[0],ids[1]);
                Button group=Find<Button>(frame,"MPW.Visual.edit.group");
                if(group==null||!group.IsEnabled)
                    throw new InvalidOperationException("两项有效选择后公共Group操作未启用");
                int beforeGroup=frame.Revision;
                group.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Revision!=beforeGroup+1||!frame.SelectedId.HasValue||
                    ids.Contains(frame.SelectedId.Value))
                    throw new InvalidOperationException("Group没有以一次工程事务创建真实组合");
                Button ungroup=Find<Button>(frame,"MPW.Visual.edit.ungroup");
                if(ungroup==null||!ungroup.IsEnabled)
                    throw new InvalidOperationException("选择真实Group后解组按钮未启用");
                ungroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Revision!=beforeGroup+2||
                    !ids.OrderBy(x=>x).SequenceEqual(frame.SceneIds.OrderBy(x=>x)))
                    throw new InvalidOperationException("解组未保留原图元身份");
                // 磁盘往返不是截图：核实Project与所有图元ID保持不变。
                frame.SaveProject(file);
                Guid projectId=frame.ProjectId;
                Guid[] saved=frame.SceneIds.ToArray();
                frame.OpenProject(file);
                if(frame.ProjectId!=projectId||!frame.SceneIds.SequenceEqual(saved)||
                    frame.IsDirty)
                    throw new InvalidOperationException("真实工程保存重开丢失对象身份");
                bool rejected=false;
                try{frame.OpenProject(file+".missing");}
                catch(FileNotFoundException){rejected=true;}
                catch(DirectoryNotFoundException){rejected=true;}
                if(!rejected||frame.ProjectId!=projectId||!frame.SceneIds.SequenceEqual(saved))
                    throw new InvalidOperationException("加载失败时没有保留原工程会话");
                frame.Inspect();
                if(!frame.StatusMessage.Contains("Revision")||
                    frame.StatusMessage.Contains("GE Passed"))
                    throw new InvalidOperationException("观察必须展示真实Revision，不伪造测试Passed");
                Console.WriteLine("PASS TCT Graphics WPF STA: MPW, scene/tree, geometry, Group/Ungroup, invalid input, MEM Undo, disk roundtrip");
            }
            finally
            {
                window.Close();frame.OnWorkspaceClosed();
                if(File.Exists(file))File.Delete(file);
            }
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