using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Mechanism.Core;
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
                // 对象树持有Group与Scene的UI行投影，不假定所有行都是MVISceneItem。
                objects.SelectedIndex=0;
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
                CaptureRealFrameIfRequested(window);
                Button group=Find<Button>(frame,"MPW.Visual.edit.group");
                if(group==null||!group.IsEnabled)
                    throw new InvalidOperationException("两项有效选择后公共Group操作未启用");
                int beforeGroup=frame.Revision;
                MEMNode[] originals=ids.Select(frame.FindObject).ToArray();
                group.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Revision!=beforeGroup+1||!frame.SelectedId.HasValue||
                    ids.Contains(frame.SelectedId.Value))
                    throw new InvalidOperationException("Group没有以一次工程事务创建真实组合");
                Guid groupId=frame.SelectedId.Value;
                MRCGroup groupObject=frame.FindObject(groupId) as MRCGroup;
                if(groupObject==null||!originals.All(item=>
                    ReferenceEquals(frame.FindObject(item.Id),item)&&item.Parent==groupObject))
                    throw new InvalidOperationException("Group未保持Core索引及原子图元对象身份");
                CheckSelection(frame,groupId);
                // Group 是工程树节点，不是可渲染的第三个Shape；右侧必须能选中它。
                bool hasGroup=objects.Items.Cast<object>().Any(row=>{
                    var property=row.GetType().GetProperty("Name");
                    string name=property==null?null:property.GetValue(row,null) as string;
                    return name!=null&&name.StartsWith("[组合]",StringComparison.Ordinal);
                });
                if(!hasGroup||objects.Items.Count!=3)
                    throw new InvalidOperationException("Group提交后工程对象树未出现组合容器");
                if(frame.Canvas.SelectedVisualIds.Count!=0)
                    throw new InvalidOperationException("Group不能伪造为普通Shape选区");
                // 工程容器虽然不投影为Shape，却能通过同一局部几何属性编辑通道操作。
                TextBox groupX=Find<TextBox>(frame,"TCT.Graphics.Geometry.X");
                if(groupX==null||!groupX.IsEnabled)
                    throw new InvalidOperationException("Group工程选择未开放正式几何编辑");
                int beforeGroupMove=frame.Revision;
                double x0=double.Parse(groupX.Text,System.Globalization.CultureInfo.InvariantCulture);
                groupX.Text=(x0+20).ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Revision!=beforeGroupMove+1)
                    throw new InvalidOperationException("Group移动未在一次MEM事务内提交");
                frame.Execute("edit.undo");
                if(frame.Revision!=beforeGroupMove+2||!frame.SelectedId.HasValue)
                    throw new InvalidOperationException("Group几何事务的Undo失败");
                Button ungroup=Find<Button>(frame,"MPW.Visual.edit.ungroup");
                if(ungroup==null||!ungroup.IsEnabled)
                    throw new InvalidOperationException("选择真实Group后解组按钮未启用");
                int beforeGroupHistory=frame.Revision;
                frame.Execute("edit.undo");
                if(frame.Revision!=beforeGroupHistory+1||frame.FindObject(groupId)!=null||ungroup.IsEnabled)
                    throw new InvalidOperationException("Undo Group的Revision、索引或可用性错误");
                CheckSelection(frame);
                frame.Execute("edit.redo");
                if(frame.Revision!=beforeGroupHistory+2||!ReferenceEquals(frame.FindObject(groupId),groupObject))
                    throw new InvalidOperationException("Redo Group未恢复同一Group对象身份");
                CheckSelection(frame);
                frame.SelectObjects(groupId);
                CheckSelection(frame,groupId);
                if(frame.Revision!=beforeGroupHistory+2||!ungroup.IsEnabled)
                    throw new InvalidOperationException("Group重选改变Revision或未恢复解组可用性");
                ungroup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Revision!=beforeGroupHistory+3||frame.FindObject(groupId)!=null||
                    !ids.OrderBy(id=>id).SequenceEqual(frame.SceneIds.OrderBy(id=>id)))
                    throw new InvalidOperationException("解组未保留原图元身份");
                CheckSelection(frame);
                frame.Execute("edit.undo");
                if(frame.Revision!=beforeGroupHistory+4||!ReferenceEquals(frame.FindObject(groupId),groupObject)||
                    !originals.All(item=>ReferenceEquals(frame.FindObject(item.Id),item)&&
                        item.Parent==groupObject))
                    throw new InvalidOperationException("Undo Ungroup未恢复同一Group及原子项对象");
                frame.SelectObjects(groupId);
                CheckSelection(frame,groupId);
                if(!ungroup.IsEnabled||frame.Revision!=beforeGroupHistory+4)
                    throw new InvalidOperationException("Undo Ungroup后选区或Revision不一致");
                frame.Execute("edit.redo");
                if(frame.Revision!=beforeGroupHistory+5||frame.FindObject(groupId)!=null||ungroup.IsEnabled||
                    !originals.All(item=>ReferenceEquals(frame.FindObject(item.Id),item)&&
                        item.Parent!=groupObject)||
                    !ids.OrderBy(id=>id).SequenceEqual(frame.SceneIds.OrderBy(id=>id)))
                    throw new InvalidOperationException("Redo Ungroup未保持身份、Revision及操作可用性");
                CheckSelection(frame);
                // 清空仍然存在的Group/Element选区也必须同步树，且不产生工程历史。
                frame.SelectObjects(ids[0]);CheckSelection(frame,ids[0]);
                frame.SelectObjects();CheckSelection(frame);
                if(frame.Revision!=beforeGroupHistory+5)
                    throw new InvalidOperationException("选区同步不应增加工程Revision");
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
        private static void CheckSelection(TCTManualGraphicsFrame frame,params Guid[] expected)
        {
            Guid? primary=expected.Length==0?(Guid?)null:expected[expected.Length-1];
            bool isGroup=primary.HasValue&&frame.FindObject(primary.Value) is MRCGroup;
            Guid[] visible=isGroup?new Guid[0]:expected;
            if(frame.SelectedId!=primary||
                !frame.TreeSelectedIds.OrderBy(id=>id).SequenceEqual(expected.OrderBy(id=>id))||
                !frame.Canvas.SelectedVisualIds.OrderBy(id=>id).SequenceEqual(visible.OrderBy(id=>id))||
                expected.Any(id=>frame.FindObject(id)==null))
                throw new InvalidOperationException("MVI主选区、画布叶选区、对象树及Core索引不一致");
        }
        /// <summary>
        /// 保存由真实WPF Window/SceneView渲染的像素证据，不使用设计示意图冒充运行截图。
        /// 用户可用CI artifact直接观察操作面板、可选画布与选区反馈。
        /// </summary>
        private static void CaptureRealFrameIfRequested(Window window)
        {
            string path=Environment.GetEnvironmentVariable("CXVISUAL_GRAPHICS_UI_SCREENSHOT");
            if(string.IsNullOrWhiteSpace(path))return;
            window.UpdateLayout();
            int width=(int)Math.Ceiling(window.ActualWidth);
            int height=(int)Math.Ceiling(window.ActualHeight);
            if(width<400||height<300)throw new InvalidOperationException("图元工作台窗口布局尺寸异常");
            RenderTargetBitmap bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
            bitmap.Render(window);
            PngBitmapEncoder encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            string directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            using(FileStream output=new FileStream(path,FileMode.Create,FileAccess.Write))
                encoder.Save(output);
            if(new FileInfo(path).Length<1000)
                throw new InvalidOperationException("图元WPF截图尺寸过小，不能作为真实画布证据");
            Console.WriteLine("PASS TCT Graphics WPF real screenshot: "+path);
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
