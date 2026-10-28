using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Geometry;
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
                if(frame.SceneCount!=0||frame.Revision!=before||
                    frame.Canvas.ActiveDrawTool!="Rect")
                    throw new InvalidOperationException("单击工具应只激活Draw，不能偷偷插入默认矩形");
                frame.Canvas.BeginDrawGesture(35,60);
                frame.Canvas.UpdateDrawGesture(165,125);
                if(!frame.Canvas.HasDrawPreview||frame.Revision!=before)
                    throw new InvalidOperationException("鼠标拖绘Ghost不应修改MEM Revision");
                if(!frame.Canvas.CompleteDrawGesture(165,125)||frame.SceneCount!=1||
                    frame.Revision!=before+1||frame.Canvas.HasDrawPreview)
                    throw new InvalidOperationException("拖绘松开没有一次提交真实MEM矩形");
                var rectShape=frame.FindObject(frame.SceneIds[0]) as MRCVisualItem;
                if(rectShape==null||Math.Abs(rectShape.X-25)>1e-6||
                    Math.Abs(rectShape.Y-50)>1e-6||
                    Math.Abs(rectShape.Width-150)>1e-6||
                    Math.Abs(rectShape.Height-75)>1e-6)
                    throw new InvalidOperationException("World网格吸附与正式Shape几何尺寸不一致");
                frame.Execute("Circle");
                if(frame.SceneCount!=1||frame.Canvas.ActiveDrawTool!="Circle")
                    throw new InvalidOperationException("圆工具应激活而不能立即创建默认圆");
                frame.Canvas.BeginDrawGesture(280,130);
                frame.Canvas.UpdateDrawGesture(367,206);
                if(!frame.Canvas.CompleteDrawGesture(367,206)||frame.SceneCount!=2)
                    throw new InvalidOperationException("圆工具鼠标拖绘提交失败");
                var round=frame.FindObject(frame.SceneIds[1]) as MRCVisualItem;
                if(round==null||Math.Abs(round.Width-round.Height)>1e-6)
                    throw new InvalidOperationException("Circle拖绘没有保持圆形约束");
                frame.Execute("Select");
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
                // 用户从公共按钮复制两个图元，必须执行Core Transfer而非绘制两张假图。
                frame.SelectObjects(saved);
                Button duplicate=Find<Button>(frame,"MPW.Visual.edit.duplicate");
                if(duplicate==null||!duplicate.IsEnabled)
                    throw new InvalidOperationException("有真实图元多选却未启用Duplicate");
                int beforeDuplicate=frame.Revision;
                duplicate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Revision!=beforeDuplicate+1||frame.SceneCount!=4||
                    !saved.All(id=>frame.SceneIds.Contains(id)))
                    throw new InvalidOperationException("WPF Duplicate没有通过一次MEM事务复制图元");
                Guid[] duplicatedIds=frame.SceneIds.Where(id=>!saved.Contains(id)).ToArray();
                if(duplicatedIds.Length!=2||!duplicatedIds.All(id=>frame.FindObject(id)!=null))
                    throw new InvalidOperationException("WPF Duplicate没有形成新的工程对象身份");
                frame.Execute("edit.undo");
                if(frame.SceneCount!=2||duplicatedIds.Any(id=>frame.FindObject(id)!=null))
                    throw new InvalidOperationException("WPF Duplicate Undo未原子删除复制对象");
                frame.Execute("edit.redo");
                if(frame.SceneCount!=4||duplicatedIds.Any(id=>frame.FindObject(id)==null))
                    throw new InvalidOperationException("WPF Duplicate Redo未恢复相同复制对象身份");
                frame.SaveProject(file);
                frame.OpenProject(file);
                if(frame.SceneCount!=4||duplicatedIds.Any(id=>frame.FindObject(id)==null))
                    throw new InvalidOperationException("WPF Duplicate保存重开身份丢失");
                // 线段与逐点多边形也必须是同一Draw手势，不是预设shape按钮。
                frame.Execute("Line");
                int lineBefore=frame.Revision;
                frame.Canvas.BeginDrawGesture(420,60);
                frame.Canvas.UpdateDrawGesture(490,160);
                if(!frame.Canvas.CompleteDrawGesture(490,160)||frame.SceneCount!=5||
                    frame.Revision!=lineBefore+1)
                    throw new InvalidOperationException("Line drag not committed as one MEM action");
                frame.Execute("Polyline");
                int pathBefore=frame.Revision;
                frame.Canvas.BeginDrawGesture(520,60);
                frame.Canvas.UpdateDrawGesture(560,90);
                frame.Canvas.BeginDrawGesture(560,90);
                frame.Canvas.BeginDrawGesture(610,160);
                if(frame.Revision!=pathBefore||
                    !frame.Canvas.CompleteDrawGesture(610,160)||frame.SceneCount!=6||
                    frame.Revision!=pathBefore+1)
                    throw new InvalidOperationException("Polyline multi-click editing did not submit once");
                frame.Execute("Polygon");
                int polygonBefore=frame.Revision;
                frame.Canvas.BeginDrawGesture(650,80);
                frame.Canvas.BeginDrawGesture(700,90);
                frame.Canvas.BeginDrawGesture(670,170);
                if(!frame.Canvas.CompleteDrawGesture(670,170)||frame.SceneCount!=7||
                    frame.Revision!=polygonBefore+1)
                    throw new InvalidOperationException("Polygon multi-click drawing failed");
                frame.Execute("Rect");
                int canceled=frame.Revision;
                frame.Canvas.BeginDrawGesture(700,300);
                frame.Canvas.UpdateDrawGesture(775,375);
                frame.Canvas.CancelInteraction();
                if(frame.Canvas.HasDrawPreview||frame.Revision!=canceled||frame.SceneCount!=7)
                    throw new InvalidOperationException("显式取消绘制不得留下Shape或Revision");
                // GE-12：连续点绘没有MouseCapture，真实WPF工作区隐藏仍需清除Ghost。
                frame.Execute("Polyline");
                int beforeHide=frame.Revision;
                frame.Canvas.BeginDrawGesture(705,305);
                frame.Canvas.UpdateDrawGesture(755,335);
                frame.Canvas.BeginDrawGesture(755,335);
                if(!frame.Canvas.HasDrawPreview)
                    throw new InvalidOperationException("多点绘制没有临时Ghost");
                frame.Canvas.Visibility=Visibility.Collapsed;
                if(frame.Canvas.HasDrawPreview||frame.Revision!=beforeHide||
                    frame.SceneCount!=7)
                    throw new InvalidOperationException("GE-12工作区隐藏未取消多点Ghost或污染MEM");
                frame.Canvas.Visibility=Visibility.Visible;
                frame.Canvas.BeginDrawGesture(710,330);
                frame.Execute("Polygon");
                if(frame.Canvas.HasDrawPreview||frame.Revision!=beforeHide||
                    frame.SceneCount!=7)
                    throw new InvalidOperationException("GE-12切换绘制工具未清除旧手势");
                frame.Execute("Text");
                int beforeText=frame.Revision;
                if(frame.Canvas.ActiveDrawTool!="Text"||frame.SceneCount!=7)
                    throw new InvalidOperationException("Text点击应激活画布放置而非自动生成文本");
                frame.Canvas.BeginDrawGesture(800,210);
                frame.Canvas.UpdateDrawGesture(950,280);
                if(!frame.Canvas.CompleteDrawGesture(950,280)||frame.SceneCount!=8||
                    frame.Revision!=beforeText+1)
                    throw new InvalidOperationException("Text拖框没有生成正式MEM文本图元");
                MRCVisualItem textItem=frame.FindObject(frame.SceneIds.Last()) as MRCVisualItem;
                if(textItem==null||Math.Abs(textItem.Width-150)>1e-6||
                    Math.Abs(textItem.Height-75)>1e-6)
                    throw new InvalidOperationException("Text画布Ghost尺寸与MEM对象不一致");
                // 已有文本直接在画布原位改写，输入预览不改Revision，Esc也不污染历史。
                frame.Execute("Select");
                Guid textId=textItem.Id;
                int beforeTextEdit=frame.Revision;
                if(!frame.Canvas.BeginInlineTextEdit(textId)||!frame.Canvas.IsInlineTextEditing)
                    throw new InvalidOperationException("文字没有进入共用SceneView原位编辑");
                TextBox inline=Find<TextBox>(frame,"MPW.Visual.InlineText");
                if(inline==null)throw new InvalidOperationException("缺少真正的WPF文字输入框");
                inline.Text="不保存的编辑";
                if(frame.Revision!=beforeTextEdit||frame.Canvas.FinishInlineTextEdit(false)||
                    frame.Revision!=beforeTextEdit||frame.Canvas.IsInlineTextEditing||
                    ((MRCTextElement)frame.FindObject(textId)).Text!="文本")
                    throw new InvalidOperationException("Esc取消文字编辑不得修改MEM对象/Revision");
                if(!frame.Canvas.BeginInlineTextEdit(textId))
                    throw new InvalidOperationException("文字无法再次进入原位编辑");
                inline=Find<TextBox>(frame,"MPW.Visual.InlineText");
                if(inline==null||!frame.Canvas.IsInlineTextEditing)
                    throw new InvalidOperationException("旧TextBox失焦误关闭第二次原位编辑");
                inline.Text="可撤销的文字更新";
                if(frame.Revision!=beforeTextEdit||!frame.Canvas.FinishInlineTextEdit(true)||
                    frame.Revision!=beforeTextEdit+1||
                    ((MRCTextElement)frame.FindObject(textId)).Text!="可撤销的文字更新")
                    throw new InvalidOperationException("文本输入未按一次MEM事务提交");
                frame.Execute("edit.undo");
                if(((MRCTextElement)frame.FindObject(textId)).Text!="文本")
                    throw new InvalidOperationException("文本更新Undo没有恢复原始内容");
                frame.Execute("edit.redo");
                if(((MRCTextElement)frame.FindObject(textId)).Text!="可撤销的文字更新")
                    throw new InvalidOperationException("文本更新Redo没有恢复修改");
                // Widget工具须先激活Draw，真实画布绘制后才有一个正式MEM对象。
                frame.Execute("WidgetButton");
                int widgetBefore=frame.Revision;
                if(frame.Canvas.ActiveDrawTool!="WidgetButton"||frame.SceneCount!=8)
                    throw new InvalidOperationException("Widget工具不得点击即插入默认控件");
                frame.Canvas.BeginDrawGesture(200,425);
                frame.Canvas.UpdateDrawGesture(340,485);
                if(!frame.Canvas.HasDrawPreview||frame.Revision!=widgetBefore)
                    throw new InvalidOperationException("Widget Ghost不应修改工程Revision");
                if(!frame.Canvas.CompleteDrawGesture(340,485)||frame.SceneCount!=9||
                    frame.Revision!=widgetBefore+1)
                    throw new InvalidOperationException("Widget拖绘未按一次MEM事务提交");
                MRCWidgetElement widget=frame.FindObject(frame.SceneIds.Last()) as MRCWidgetElement;
                if(widget==null||widget.WidgetTypeId!="core.widget"||
                    Math.Abs(widget.Width-150)>1e-6||Math.Abs(widget.Height-50)>1e-6)
                    throw new InvalidOperationException("Widget Ghost与正式控件位置尺寸不一致");
                frame.Execute("core.widget.textbox");
                frame.Canvas.BeginDrawGesture(720,475);
                if(!frame.Canvas.CompleteDrawGesture(720,475)||frame.SceneCount!=10)
                    throw new InvalidOperationException("Widget单击定位失败");
                MRCWidgetElement textbox=frame.FindObject(frame.SceneIds.Last()) as MRCWidgetElement;
                if(textbox==null||textbox.WidgetTypeId!="core.widget.textbox"||
                    Math.Abs(textbox.Width-120)>1e-6||Math.Abs(textbox.Height-36)>1e-6)
                    throw new InvalidOperationException("Widget单击尺寸不符合同一纯布局规则");
                Guid widgetId=widget.Id,textboxId=textbox.Id;
                frame.Execute("Select");
                // GE-13图库原生DragDrop最终走此统一画布入口；禁止接收未知TypeId。
                int dropBefore=frame.Revision;
                if(!frame.Canvas.AllowDrop||
                    frame.Canvas.DropWidgetFromPalette("core.widget.invalid",375,400)||
                    frame.Revision!=dropBefore||frame.SceneCount!=10)
                    throw new InvalidOperationException("非法Palette控件不能生成MEM事务");
                if(!frame.Canvas.DropWidgetFromPalette("core.widget.checkbox",375,400)||
                    frame.Revision!=dropBefore+1||frame.SceneCount!=11)
                    throw new InvalidOperationException("Palette Widget没有一次提交");
                MRCWidgetElement dropped=frame.FindObject(frame.SceneIds.Last()) as MRCWidgetElement;
                if(dropped==null||dropped.WidgetTypeId!="core.widget.checkbox"||
                    Math.Abs(dropped.Width-120)>1e-6||Math.Abs(dropped.Height-36)>1e-6)
                    throw new InvalidOperationException("Palette TypeId/单击默认尺寸不一致");
                Guid droppedId=dropped.Id;
                frame.SaveProject(file);
                frame.OpenProject(file);
                if(frame.SceneCount!=11||frame.IsDirty||
                    !(frame.FindObject(widgetId) is MRCWidgetElement)||
                    !(frame.FindObject(textboxId) is MRCWidgetElement)||
                    !(frame.FindObject(droppedId) is MRCWidgetElement)||
                    ((MRCTextElement)frame.FindObject(textId)).Text!="可撤销的文字更新")
                    throw new InvalidOperationException("Widget绘制保存重开或身份不正确");
                // GE-03: 真实公共WPF Scene内进入D几何编辑，以世界命中分割Bezier。
                frame.Execute("Bezier");
                frame.Canvas.BeginDrawGesture(180,480);
                frame.Canvas.UpdateDrawGesture(510,610);
                if(!frame.Canvas.CompleteDrawGesture(510,610))
                    throw new InvalidOperationException("Bezier画布绘制没有落到MEM");
                Guid bezierId=frame.SceneIds.Last();
                frame.Execute("Select");
                frame.SelectObjects(bezierId);
                MRCShapeElement curve=frame.FindObject(bezierId) as MRCShapeElement;
                if(curve==null||curve.Shape.Kind!=MRCShapeKind.Bezier)
                    throw new InvalidOperationException("需要正式Bezier工程对象");
                if(!frame.Canvas.EnterGeometryEditModeForSelection())
                    throw new InvalidOperationException("Bezier无法进入D几何编辑态");
                var projectedCurve=new CxVisual.Mechanism.Visual.MVISceneItem(curve);
                var sourcePath=curve.Shape.Geometry;
                FGEBezier2D cubic=new FGEBezier2D(sourcePath.Commands[0].End,
                    sourcePath.Commands[1].Control1,sourcePath.Commands[1].Control2,
                    sourcePath.Commands[1].End);
                FGEPoint2D at=projectedCurve.GeometryToWorld.TransformPoint(cubic.PointAt(0.43));
                int beforeSplit=frame.Revision;
                if(frame.Canvas.RequestSelectedPathSplitAt(at.X+1000,at.Y+1000)||
                    frame.Revision!=beforeSplit||curve.Shape.Geometry.Commands.Count!=2)
                    throw new InvalidOperationException("未命中的双击不得创建几何历史");
                ContextMenu curveMenu=frame.Canvas.CreateSelectedGeometryMenuAt(at.X,at.Y);
                if(curveMenu==null||!curveMenu.Items.OfType<MenuItem>().Any(m=>
                    (string)m.Header=="转为直线段")||
                    !curveMenu.Items.OfType<MenuItem>().Any(m=>
                    (string)m.Header=="精确插入锚点"))
                    throw new InvalidOperationException("D几何右键没有暴露曲线转换与精确插点");
                if(!frame.Canvas.RequestSelectedPathSplitAt(at.X,at.Y)||
                    frame.Revision!=beforeSplit+1||curve.Shape.Geometry.Commands.Count!=3)
                    throw new InvalidOperationException("曲线原位插点没有一次MEM事务");
                frame.Execute("edit.undo");
                if(curve.Shape.Geometry.Commands.Count!=2)
                    throw new InvalidOperationException("Bezier插点Undo没有恢复原曲线");
                frame.Execute("edit.redo");
                if(curve.Shape.Geometry.Commands.Count!=3)
                    throw new InvalidOperationException("Bezier插点Redo没有恢复精确曲线");
                frame.SaveProject(file);frame.OpenProject(file);
                if(!(frame.FindObject(bezierId) is MRCShapeElement)||
                    ((MRCShapeElement)frame.FindObject(bezierId)).Shape.Geometry.Commands.Count!=3)
                    throw new InvalidOperationException("Bezier插点保存重开丢失了路径几何");
                frame.Execute("Select");frame.SelectObjects(bezierId);
                if(!frame.Canvas.EnterGeometryEditModeForSelection())
                    throw new InvalidOperationException("路径编辑在保存重开后不可重新进入");
                MRCShapeElement persisted=(MRCShapeElement)frame.FindObject(bezierId);
                int originalRevision=frame.Revision;
                if(!frame.Canvas.RequestSelectedPathOperation(bezierId,1,"line")||
                    frame.Revision!=originalRevision+1||
                    persisted.Shape.Geometry.Commands[1].Verb!=FGEPathVerb.Line)
                    throw new InvalidOperationException("Bezier首段右键转直线没有一次MEM事务");
                frame.Execute("edit.undo");
                if(persisted.Shape.Geometry.Commands[1].Verb!=FGEPathVerb.Cubic)
                    throw new InvalidOperationException("曲线转直线Undo未恢复原精确Bezier");
                frame.Execute("edit.redo");
                if(persisted.Shape.Geometry.Commands[1].Verb!=FGEPathVerb.Line)
                    throw new InvalidOperationException("曲线转直线Redo未恢复段类型");

                // GE-03/12/13：Backspace只回退WPF草稿点，不能误操作MEM的Undo栈。
                frame.Execute("Polyline");
                int backspaceBefore=frame.Revision,backspaceCount=frame.SceneCount;
                frame.Canvas.BeginDrawGesture(110,430);
                frame.Canvas.BeginDrawGesture(175,445);
                frame.Canvas.BeginDrawGesture(225,480);
                PressKey(frame.Canvas,Key.Back);
                if(!frame.Canvas.HasDrawPreview||frame.Revision!=backspaceBefore||
                    frame.SceneCount!=backspaceCount)
                    throw new InvalidOperationException("Polyline撤销草稿顶点不能修改工程");
                PressKey(frame.Canvas,Key.Enter);
                if(frame.Canvas.HasDrawPreview||
                    frame.Revision!=backspaceBefore+1||
                    frame.SceneCount!=backspaceCount+1||
                    ((MRCShapeElement)frame.FindObject(frame.SceneIds.Last()))
                        .Shape.Geometry.Commands.Count!=2)
                    throw new InvalidOperationException("Polyline撤销最后一点后提交的几何不正确");
                frame.Execute("Polygon");
                int polygonRevision=frame.Revision,polygonCount=frame.SceneCount;
                frame.Canvas.BeginDrawGesture(270,400);
                frame.Canvas.BeginDrawGesture(340,400);
                frame.Canvas.BeginDrawGesture(350,470);
                PressKey(frame.Canvas,Key.Back);
                PressKey(frame.Canvas,Key.Enter);
                if(!frame.Canvas.HasDrawPreview||frame.Revision!=polygonRevision||
                    frame.SceneCount!=polygonCount)
                    throw new InvalidOperationException("Polygon删点后不足三点，Enter不得部分提交");
                frame.Canvas.BeginDrawGesture(320,465);
                PressKey(frame.Canvas,Key.Enter);
                if(frame.Canvas.HasDrawPreview||frame.Revision!=polygonRevision+1||
                    frame.SceneCount!=polygonCount+1)
                    throw new InvalidOperationException("Polygon补点后应一次提交");
                frame.Execute("Path");
                int contourBackRevision=frame.Revision,contourBackCount=frame.SceneCount;
                frame.Canvas.BeginDrawGesture(420,405);
                frame.Canvas.BeginDrawGesture(480,435);
                PressKey(frame.Canvas,Key.N);
                PressKey(frame.Canvas,Key.Back); // 先撤销尚未开始的新轮廓。
                PressKey(frame.Canvas,Key.N);
                frame.Canvas.BeginDrawGesture(590,440);
                PressKey(frame.Canvas,Key.Back); // 第二轮廓唯一顶点退回待建态。
                PressKey(frame.Canvas,Key.Enter);
                if(frame.Canvas.HasDrawPreview||frame.Revision!=contourBackRevision+1||
                    frame.SceneCount!=contourBackCount+1||
                    ((MRCShapeElement)frame.FindObject(frame.SceneIds.Last()))
                        .Shape.Geometry.FlattenContours().Count!=1)
                    throw new InvalidOperationException("Path回退第二轮廓后Enter应保留合法首轮廓");
                frame.Execute("Path");
                int emptyBackRevision=frame.Revision,emptyBackCount=frame.SceneCount;
                frame.Canvas.BeginDrawGesture(610,430);
                PressKey(frame.Canvas,Key.Back);
                if(frame.Canvas.HasDrawPreview||frame.Revision!=emptyBackRevision||
                    frame.SceneCount!=emptyBackCount)
                    throw new InvalidOperationException("回退最后草稿顶点必须彻底清除Ghost");
                PressKey(frame.Canvas,Key.Enter);
                if(frame.Revision!=emptyBackRevision||frame.SceneCount!=emptyBackCount)
                    throw new InvalidOperationException("空草稿Enter不得生成MEM工程对象");
                // 真实WPF键盘路由：N只预备新轮廓，若直接Enter，已完成的
                // 第一轮廓仍须一次提交，不能留下永远无法收尾的空草稿。
                frame.Execute("Path");
                int pendingBefore=frame.Revision,pendingCount=frame.SceneCount;
                frame.Canvas.BeginDrawGesture(150,500);
                frame.Canvas.BeginDrawGesture(200,550);
                PressKey(frame.Canvas,Key.N);
                if(!frame.Canvas.HasDrawPreview||frame.Revision!=pendingBefore)
                    throw new InvalidOperationException("N准备轮廓修改了Revision或丢失Ghost");
                PressKey(frame.Canvas,Key.Enter);
                if(frame.Canvas.HasDrawPreview||frame.Revision!=pendingBefore+1||
                    frame.SceneCount!=pendingCount+1)
                    throw new InvalidOperationException("N后空轮廓Enter没有完成合法Path");
                var pendingPath=(MRCShapeElement)frame.FindObject(frame.SceneIds.Last());
                if(pendingPath.Shape.Geometry.Commands.Count!=2||
                    pendingPath.Shape.Geometry.FlattenContours().Count!=1)
                    throw new InvalidOperationException("空的新轮廓不得生成多余MoveTo");
                // N后只画一个新轮廓顶点时Enter必须拒绝；Esc彻底取消草稿。
                frame.Execute("Path");
                int invalidBefore=frame.Revision,invalidCount=frame.SceneCount;
                frame.Canvas.BeginDrawGesture(150,500);
                frame.Canvas.BeginDrawGesture(200,550);
                PressKey(frame.Canvas,Key.N);
                frame.Canvas.BeginDrawGesture(350,525);
                PressKey(frame.Canvas,Key.Enter);
                if(!frame.Canvas.HasDrawPreview||frame.Revision!=invalidBefore)
                    throw new InvalidOperationException("单点第二轮廓Enter不得部分提交");
                PressKey(frame.Canvas,Key.Escape);
                if(frame.Canvas.HasDrawPreview||frame.Revision!=invalidBefore||
                    frame.SceneCount!=invalidCount)
                    throw new InvalidOperationException("多轮廓Esc取消残留Ghost或污染MEM");
                // Path连续点绘按N新增第二个子轮廓，不应生成隐式跨轮廓连接。
                frame.Execute("Path");
                int multiBefore=frame.Revision;
                frame.Canvas.BeginDrawGesture(140,220);
                frame.Canvas.BeginDrawGesture(200,245);
                frame.Canvas.BeginDrawGesture(250,215);
                if(!frame.Canvas.BeginNextPathContour()||frame.Revision!=multiBefore)
                    throw new InvalidOperationException("Path新轮廓草稿不得提前提交MEM");
                frame.Canvas.BeginDrawGesture(390,280);
                if(frame.Canvas.CompleteDrawGesture(390,280)||frame.Revision!=multiBefore)
                    throw new InvalidOperationException("单点第二轮廓不得提交部分Path");
                frame.Canvas.BeginDrawGesture(460,310);
                if(!frame.Canvas.CompleteDrawGesture(460,310)||
                    frame.Revision!=multiBefore+1)
                    throw new InvalidOperationException("Path双轮廓没有单次MEM提交");
                Guid multiId=frame.SceneIds.Last();
                MRCShapeElement multi=(MRCShapeElement)frame.FindObject(multiId);
                if(multi.Shape.Kind!=MRCShapeKind.Path||
                    multi.Shape.Geometry.FlattenContours().Count!=2||
                    multi.Shape.Geometry.Commands.Count!=5||
                    multi.Shape.Geometry.Commands[3].Verb!=FGEPathVerb.Move)
                    throw new InvalidOperationException("双轮廓Move命令被错误连成一条Path");
                frame.Execute("edit.undo");
                if(frame.FindObject(multiId)!=null)
                    throw new InvalidOperationException("多轮廓Path未按单次Undo删除");
                frame.Execute("edit.redo");
                if(!(frame.FindObject(multiId) is MRCShapeElement))
                    throw new InvalidOperationException("多轮廓Path Redo对象身份丢失");
                frame.SaveProject(file);frame.OpenProject(file);
                if(!(((MRCShapeElement)frame.FindObject(multiId)).Shape.Geometry
                    .FlattenContours().Count==2))
                    throw new InvalidOperationException("多轮廓Path保存重开合并了子轮廓");
                frame.Execute("Select");frame.SelectObjects(multiId);
                if(!frame.Canvas.EnterGeometryEditModeForSelection())
                    throw new InvalidOperationException("重开后多轮廓Path不能进入D编辑");
                int contourRevision=frame.Revision;
                if(frame.Canvas.RequestSelectedPathOperation(multiId,4,"close")||
                    frame.Revision!=contourRevision)
                    throw new InvalidOperationException("两点子轮廓不应允许闭合");
                ContextMenu multiMenu=frame.Canvas.CreateSelectedGeometryMenuAt(170,232.5);
                if(multiMenu==null||!multiMenu.Items.OfType<MenuItem>().Any(entry=>
                    (string)entry.Header=="闭合此轮廓"))
                    throw new InvalidOperationException("首子轮廓右键菜单应显示合法的闭合命令");
                if(!frame.Canvas.RequestSelectedPathOperation(multiId,1,"close")||
                    frame.Revision!=contourRevision+1||
                    ((MRCShapeElement)frame.FindObject(multiId)).Shape.Geometry
                        .Commands[3].Verb!=FGEPathVerb.Close)
                    throw new InvalidOperationException("关闭首轮廓不能跨轮廓或者分成多个事务");
                frame.Execute("edit.undo");
                if(((MRCShapeElement)frame.FindObject(multiId)).Shape.Geometry
                    .Commands.Count!=5)
                    throw new InvalidOperationException("多轮廓闭合Undo未恢复原路径");
                frame.Execute("edit.redo");
                if(((MRCShapeElement)frame.FindObject(multiId)).Shape.Geometry
                    .Commands[3].Verb!=FGEPathVerb.Close)
                    throw new InvalidOperationException("多轮廓闭合Redo丢失Close");
                frame.SaveProject(file);frame.OpenProject(file);
                if(((MRCShapeElement)frame.FindObject(multiId)).Shape.Geometry
                    .FlattenContours().Count!=2)
                    throw new InvalidOperationException("闭合后的多轮廓磁盘往返丢失子路径");

                // GE-02：重叠图元Alt+单击轮换，选区/对象树/MVI保持同一身份，
                // 每次仅修改UI选中状态，不形成MEM修订或Undo。
                frame.Execute("Rect");
                frame.Canvas.BeginDrawGesture(2000,2000);
                frame.Canvas.UpdateDrawGesture(2125,2100);
                if(!frame.Canvas.CompleteDrawGesture(2125,2100))
                    throw new InvalidOperationException("重叠选择前的底层矩形创建失败");
                Guid lowerId=frame.SceneIds.Last();
                // 两个空心矩形共用同一条描边，内部空白不得被误当作命中区域。
                frame.Canvas.BeginDrawGesture(2000,2000);
                frame.Canvas.UpdateDrawGesture(2125,2100);
                if(!frame.Canvas.CompleteDrawGesture(2125,2100))
                    throw new InvalidOperationException("重叠选择前的顶层矩形创建失败");
                Guid upperId=frame.SceneIds.Last();
                frame.Execute("Select");
                frame.SelectObjects();
                int overlapRevision=frame.Revision,overlapCount=frame.SceneCount;
                if(!frame.Canvas.SelectNextOverlappedAt(2050,2000))
                    throw new InvalidOperationException("重叠选择首击未找到图元");
                CheckSelection(frame,upperId);
                if(!frame.Canvas.SelectNextOverlappedAt(2050,2000))
                    throw new InvalidOperationException("重叠选择不能到达下层图元");
                CheckSelection(frame,lowerId);
                if(!frame.Canvas.SelectNextOverlappedAt(2050,2000))
                    throw new InvalidOperationException("重叠选择不能循环回顶层");
                CheckSelection(frame,upperId);
                if(frame.Canvas.SelectNextOverlappedAt(2050,2050))
                    throw new InvalidOperationException("空心矩形内部不是有效描边，不应误命中");
                CheckSelection(frame,upperId);
                if(frame.Canvas.SelectNextOverlappedAt(5000,5000))
                    throw new InvalidOperationException("空白处不应轮换不存在的图元");
                CheckSelection(frame,upperId);
                if(frame.Revision!=overlapRevision||frame.SceneCount!=overlapCount)
                    throw new InvalidOperationException("轮换选择污染MEM修订或工程对象数量");
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
        /// <summary>派发WPF真实KeyDown路由；不冒称OS硬件输入注入。</summary>
        private static void PressKey(SceneView canvas,Key key)
        {
            PresentationSource source=PresentationSource.FromVisual(canvas);
            if(source==null)throw new InvalidOperationException("WPF画布尚未连接到PresentationSource");
            canvas.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key)
                {RoutedEvent=Keyboard.KeyDownEvent});
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
