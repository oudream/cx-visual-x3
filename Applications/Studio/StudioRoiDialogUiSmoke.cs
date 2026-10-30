using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using AvalonDock.Layout;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Visual;
using CxVisual.Professional.Vision;

namespace CxVisual.Studio
{
    /// <summary>
    /// 独立Windows验收：实际Studio WPF按钮 + 操作系统原生Open/Save对话框。
    /// 此测试不代表人工手持鼠标、多DPI或工业设备HIL；测试夹具工程由MEM创建。
    /// UIAutomation从后台线程访问，避免与WPF模态消息循环互相等待。
    /// </summary>
    internal static class StudioRoiDialogUiSmoke
    {
        [DllImport("user32.dll",SetLastError=true)]
        private static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags,uint x,uint y,uint data,
            UIntPtr extraInfo);
        private const uint MouseLeftDown=0x0002;
        private const uint MouseLeftUp=0x0004;

        private static void Require(bool passed,string message)
        {
            if(!passed)throw new InvalidOperationException("IMG05_UIA: "+message);
        }

        private static AutomationElement Find(AutomationElement root,string id)
        {
            AutomationElement element=root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty,id));
            if(element==null)throw new InvalidOperationException("IMG05_UIA 缺少控件："+id);
            return element;
        }

        private static void Invoke(IntPtr windowHandle,string automationId)
        {
            AutomationElement element=Find(AutomationElement.FromHandle(windowHandle),
                automationId);
            InvokePattern pattern=element.GetCurrentPattern(InvokePattern.Pattern)
                as InvokePattern;
            if(pattern==null)throw new InvalidOperationException(
                "IMG05_UIA 控件不可Invoke："+automationId);
            pattern.Invoke();
        }

        private static void SetValue(IntPtr windowHandle,string automationId,string value)
        {
            AutomationElement element=Find(AutomationElement.FromHandle(windowHandle),
                automationId);
            ValuePattern pattern=element.GetCurrentPattern(ValuePattern.Pattern)
                as ValuePattern;
            if(pattern==null)throw new InvalidOperationException(
                "IMG05_UIA 控件不可写："+automationId);
            pattern.SetValue(value);
        }

        private static AutomationElementCollection NativeProcessWindows()
        {
            return AutomationElement.RootElement.FindAll(TreeScope.Children,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ProcessIdProperty,
                        Process.GetCurrentProcess().Id),
                    new PropertyCondition(AutomationElement.ControlTypeProperty,
                        ControlType.Window)));
        }

        private static string NativeWindowDiagnostics()
        {
            var windows=NativeProcessWindows();
            var descriptions=new System.Collections.Generic.List<string>();
            foreach(AutomationElement window in windows)
            {
                try
                {
                    descriptions.Add("Name="+window.Current.Name+
                        " Class="+window.Current.ClassName+
                        " Hwnd="+window.Current.NativeWindowHandle+
                        " Enabled="+window.Current.IsEnabled);
                }
                catch(ElementNotAvailableException)
                { descriptions.Add("(窗口在枚举中已关闭)"); }
            }
            return descriptions.Count==0?"(当前桌面没有本进程顶层Window)":
                String.Join(" | ",descriptions);
        }

        private static AutomationElement FindNativeDialog(string title)
        {
            AutomationElement nativeCandidate=null;
            foreach(AutomationElement window in NativeProcessWindows())
            {
                try
                {
                    if(window.Current.Name.IndexOf(title,StringComparison.Ordinal)>=0)
                        return window;
                    // Win32通用文件对话框稳定窗口类；地区语言/自定义标题在
                    // UIA Name上可能不同，但必须确实是本进程原生#32770。
                    if(String.Equals(window.Current.ClassName,"#32770",
                        StringComparison.Ordinal))
                    {
                        if(nativeCandidate!=null)return null; // 多弹框不可猜
                        nativeCandidate=window;
                    }
                }
                catch(ElementNotAvailableException){}
            }
            return nativeCandidate;
        }

        private static AutomationElement FindFileNameEditor(AutomationElement dialog)
        {
            AutomationElementCollection controls=dialog.FindAll(TreeScope.Descendants,
                System.Windows.Automation.Condition.TrueCondition);
            // Windows通用文件对话框的文件名框AutomationId通常为1148；
            // 优先使用该稳定身份，其次使用其内部Edit（1001）。
            foreach(string id in new[]{"1148","1001"})
                foreach(AutomationElement element in controls)
                {
                    if(element.Current.AutomationId!=id)continue;
                    object pattern;
                    if(element.TryGetCurrentPattern(ValuePattern.Pattern,out pattern) &&
                       !((ValuePattern)pattern).Current.IsReadOnly)
                        return element;
                }
            throw new InvalidOperationException(
                "IMG05_UIA 系统文件对话框没有可写文件名框（1148/1001）");
        }

        private static void ConfirmNativeFileDialog(string title,string file)
        {
            AutomationElement dialog=null;
            try
            {
                Stopwatch clock=Stopwatch.StartNew();
                while(clock.ElapsedMilliseconds<14000 && dialog==null)
                {
                    dialog=FindNativeDialog(title);
                    if(dialog==null)Thread.Sleep(50);
                }
                Require(dialog!=null,"系统文件对话框未出现："+title+
                    "；本进程窗口快照："+NativeWindowDiagnostics());
                AutomationElement editor=FindFileNameEditor(dialog);
                ((ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern))
                    .SetValue(file);
                AutomationElement button=Find(dialog,"1");
                InvokePattern invoke=button.GetCurrentPattern(InvokePattern.Pattern)
                    as InvokePattern;
                Require(invoke!=null && button.Current.IsEnabled,
                    "系统打开/保存按钮不可点击："+title);
                invoke.Invoke();
            }
            catch
            {
                // 失败时尝试关闭模态窗口，使本轮独立验收可退出并留下FAIL报告。
                if(dialog!=null)
                    try
                    {
                        AutomationElement cancel=dialog.FindFirst(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.AutomationIdProperty,"2"));
                        if(cancel!=null)
                        {
                            InvokePattern action=cancel.GetCurrentPattern(InvokePattern.Pattern)
                                as InvokePattern;
                            if(action!=null)action.Invoke();
                        }
                    }
                    catch(InvalidOperationException){}
                    catch(System.Windows.Automation.ElementNotAvailableException){}
                throw;
            }
        }

        private static async Task ClickWithNativeDialog(IntPtr hwnd,
            string buttonId,string title,string file)
        {
            Task click=Task.Run(()=>Invoke(hwnd,buttonId));
            Task dialog=Task.Run(()=>ConfirmNativeFileDialog(title,file));
            Task pair=Task.WhenAll(click,dialog);
            if(await Task.WhenAny(pair,Task.Delay(22000))!=pair)
            {
                // Click的InvokePattern若早已抛错，优先保留真实控件异常，
                // 不应被另一Task的“没有文件弹框”14秒超时掩盖。
                if(click.IsFaulted)await click;
                if(dialog.IsFaulted)await dialog;
                throw new TimeoutException("IMG05_UIA 原生文件对话框操作超时："+title);
            }
            if(click.IsFaulted)await click;
            await pair;
        }

        private static async Task Click(IntPtr hwnd,string id)
        {
            await Task.Run(()=>Invoke(hwnd,id));
        }

        private static async Task Until(Func<bool> check,string reason)
        {
            for(int i=0;i<240;i++)
            {
                if(check())return;
                await Task.Delay(50);
            }
            throw new TimeoutException("IMG05_UIA 等待失败："+reason);
        }

        /// <summary>真实Windows系统指针拖动；不能把路由合成事件冒充鼠标输入。</summary>
        private static async Task<Guid> DrawRectangleWithNativeMouse(
            MainWindow window,MPWImageViewerControl viewer,Guid oldRoiId)
        {
            FrameworkElement viewport=viewer.FindName("ViewportRoot") as FrameworkElement;
            Require(viewport!=null,"MPW原图ViewportRoot未找到");
            viewer.BringIntoView();
            window.Activate();
            window.UpdateLayout();
            Require(viewport.IsVisible && viewport.ActualWidth>=80 &&
                viewport.ActualHeight>=80,"当前Studio ROI图窗未在有效可绘制区域显示");
            double step=Math.Min(viewport.ActualWidth,viewport.ActualHeight)*0.23;
            Point center=new Point(viewport.ActualWidth/2.0,viewport.ActualHeight/2.0);
            Point from=viewport.PointToScreen(new Point(center.X-step,center.Y-step));
            Point to=viewport.PointToScreen(new Point(center.X+step,center.Y+step));
            Point windowTop=window.PointToScreen(new Point(0,0));
            Point windowBottom=window.PointToScreen(new Point(
                window.ActualWidth,window.ActualHeight));
            Require(from.X>=windowTop.X && to.X<=windowBottom.X &&
                from.Y>=windowTop.Y && to.Y<=windowBottom.Y,
                "原图视口当前被Studio宿主裁剪，真实鼠标不能触达");
            await Task.Run(()=>{
                int x0=(int)Math.Round(from.X),y0=(int)Math.Round(from.Y);
                int x1=(int)Math.Round(to.X),y1=(int)Math.Round(to.Y);
                Require(SetCursorPos(x0,y0),"无法移动Windows系统指针到图窗");
                Thread.Sleep(100);
                bool pressed=false;
                try
                {
                    mouse_event(MouseLeftDown,0,0,0,UIntPtr.Zero);
                    pressed=true;
                    for(int i=1;i<=8;i++)
                    {
                        Require(SetCursorPos(x0+(x1-x0)*i/8,y0+(y1-y0)*i/8),
                            "真实拖框过程中Windows指针移动失败");
                        Thread.Sleep(35);
                    }
                }
                finally
                {
                    // UIA异常/鼠标移动失败时必须释放OS左键，避免影响下一个
                    // 原生文件弹窗或后续Studio/GitHub runner的真实交互验收。
                    if(pressed)mouse_event(MouseLeftUp,0,0,0,UIntPtr.Zero);
                }
            });
            await Until(()=>viewer.SelectedOverlayId.HasValue &&
                viewer.SelectedOverlayId.Value!=oldRoiId &&
                viewer.Overlays.Find(viewer.SelectedOverlayId.Value)!=null,
                "真实Windows鼠标拖动后ROI没有进入唯一MVI OverlayStore");
            MVIImageOverlayItem created=viewer.Overlays.Find(
                viewer.SelectedOverlayId.Value);
            Require(created.Kind==MVIImageOverlayKind.RectangleRoi &&
                created.IsAreaRoi && created.Bounds.Width>0 &&
                created.Bounds.Height>0,
                "实际Windows鼠标没有形成有效面积矩形ROI");
            return created.Id;
        }

        private static T HeaderControl<T>(StackPanel header,string id) where T:FrameworkElement
        {
            foreach(UIElement child in header.Children)
            {
                T direct=child as T;
                if(direct!=null &&
                   AutomationProperties.GetAutomationId(direct)==id)return direct;
                WrapPanel panel=child as WrapPanel;
                if(panel==null)continue;
                foreach(UIElement entry in panel.Children)
                {
                    T found=entry as T;
                    if(found!=null &&
                       AutomationProperties.GetAutomationId(found)==id)return found;
                }
            }
            throw new InvalidOperationException("IMG05_UIA 缺少模块面板："+id);
        }

        private static void Trace(string report,string step)
        {
            File.AppendAllText(report,
                "STEP: "+DateTime.UtcNow.ToString("O")+" "+step+
                Environment.NewLine,new UTF8Encoding(false));
        }

        public static async Task<bool> RunAsync(MainWindow window,string report)
        {
            string folder=Path.Combine(Path.GetTempPath(),
                "CxVisual-IMG05-NativeUIA-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(report,
                "START IMG05 Studio Windows native-dialog/ROI acceptance"+Environment.NewLine,
                new UTF8Encoding(false));
            try
            {
                string raw=Path.Combine(folder,"source-4x4-stride10.raw");
                string wrong=Path.Combine(folder,"wrong-pixel.raw");
                string project=Path.Combine(folder,"real-roi.cxproj");
                string csv=Path.Combine(folder,"run-evidence.csv");
                string rawExport=Path.Combine(folder,"run-mask.raw");
                byte[] pixels=new byte[40];
                for(int y=0;y<4;y++)
                {
                    for(int x=0;x<4;x++)pixels[y*10+x*2]=210;
                    pixels[y*10+8]=77;pixels[y*10+9]=88;
                }
                FIMImageBuffer original=new FIMImageBuffer(
                    4,4,FIMPixelFormat.Gray16,pixels,10);
                FIMRawImageWriter.SaveFile(raw,original,preserveStride:true);
                byte[] changed=(byte[])pixels.Clone();
                changed[0]=209;
                File.WriteAllBytes(wrong,changed);
                StudioViewModel vm=window.DataContext as StudioViewModel;
                Require(vm!=null,"真实Studio ViewModel缺失");
                MEMPage page=vm.ProjectSession.AddPage("IMG05真实模态操作工程页");
                MVIImageOverlayItem roi=new MVIImageOverlayItem(
                    MVIImageOverlayKind.PolygonRoi,MVIImageOverlayLayer.Roi,
                    new FGERect2D(0,0,3.6,3.6),
                    new[]{new FGEPoint2D(0,0),new FGEPoint2D(3.6,0),
                        new FGEPoint2D(0,3.6)},"IMG05 Triangle");
                CxVisionInspectionOptions options=new CxVisionInspectionOptions {
                    PixelDepth=CxVisionPixelDepth.Bits16,GrayThreshold=200,
                    MinDefectArea=1,MaxDefectArea=16,UseAi=false
                };
                Guid pageId=page.Id;
                Guid projectId=vm.ProjectSession.Project.Id;
                CxVisionRoiEngineering.Commit(vm.ProjectSession,page.RootContent,
                    vm.ProjectSession.Revision,original,"img05/native/file",roi,
                    options,43,CancellationToken.None);
                vm.SaveProject(project);
                vm.OpenProject(project,true);
                Require(vm.ProjectSession.Project.Id==projectId &&
                    !vm.ProjectSession.IsDirty,"原MEM工程未真正保存关闭并重开");
                vm.ActivePage=vm.Pages.Single(x=>x.Id==pageId);
                Trace(report,"MEM工程已保存、关闭并重开");
                int revision=vm.ProjectSession.Revision;
                window.OpenStudioDocument("doc.modules");
                LayoutDocument tab=window.StudioDock.Layout.ActiveContent as LayoutDocument;
                MPWModuleCenterControl module=tab==null?null:
                    tab.Content as MPWModuleCenterControl;
                Require(module!=null,"Studio没有激活真实模块中心");
                StackPanel header=module.HeaderContent as StackPanel;
                Require(header!=null,"模块中心WPF HeaderContent缺失");
                TextBlock status=HeaderControl<TextBlock>(header,
                    "Studio.ModuleCenter.RawResultStatus");
                Expander expander=HeaderControl<Expander>(header,
                    "Studio.ModuleCenter.ResultPanel");
                StackPanel stack=expander.Content as StackPanel;
                Require(stack!=null,"Run结果面板缺失");
                TextBlock details=stack.Children.OfType<TextBlock>().Single(x=>
                    AutomationProperties.GetAutomationId(x)=="Studio.ModuleCenter.ResultDetails");
                ListBox results=stack.Children.OfType<ListBox>().Single(x=>
                    AutomationProperties.GetAutomationId(x)=="Studio.ModuleCenter.ResultItems");
                MPWImageViewerControl viewer=stack.Children.OfType<MPWImageViewerControl>()
                    .Single(x=>AutomationProperties.GetAutomationId(x)==
                        "Studio.ModuleCenter.ResultViewer");
                window.UpdateLayout();
                IntPtr hwnd=new WindowInteropHelper(window).Handle;
                Require(hwnd!=IntPtr.Zero,"Studio真实HWND不存在");
                await Task.Run(()=>SetValue(hwnd,
                    "Studio.ModuleCenter.BindSavedRoiStride","-1"));
                await Click(hwnd,"Studio.ModuleCenter.BindSavedRoiRaw");
                Require(status.Text.Contains("RAW行步长必须是非负整数") &&
                    viewer.Session.Original==null &&
                    vm.ProjectSession.Revision==revision,
                    "WPF非法Stride输入必须在打开文件对话框前拒绝且不污染工程");
                await Task.Run(()=>SetValue(hwnd,
                    "Studio.ModuleCenter.BindSavedRoiStride","10"));
                Trace(report,"原生OpenFileDialog即将重绑已存ROI原像素");
                await ClickWithNativeDialog(hwnd,"Studio.ModuleCenter.BindSavedRoiRaw",
                    "重新绑定工程ROI原图",raw);
                await Until(()=>status.Text.Contains("原图校验通过")||
                    status.Text.Contains("失败"),"实际RAW文件重绑");
                Require(status.Text.Contains("原图校验通过") &&
                    Object.ReferenceEquals(viewer.Session.Original,null)==false &&
                    viewer.Session.Original.Stride==10 &&
                    vm.ProjectSession.Revision==revision &&
                    viewer.SelectedOverlayId==roi.Id &&
                    viewer.Overlays.Find(roi.Id)!=null,
                    "真实文件弹框未直接恢复正式ROI几何、像素/Stride或更改了工程Revision："+status.Text);
                // 使用真实UIAutomation按钮切换MPW工具，不再依赖测试调用ToolMode属性。
                await Click(hwnd,"Studio.ModuleCenter.DrawRectangleRoi");
                Require(viewer.ToolMode==MVIImageToolMode.RectangleRoi &&
                    viewer.Session.ActiveSlot==MVIImageSlot.Original,
                    "真实Studio未能进入MPW矩形ROI绘制模式");
                await Click(hwnd,"Studio.ModuleCenter.DrawPolygonRoi");
                Require(viewer.ToolMode==MVIImageToolMode.PolygonRoi,
                    "真实Studio未能进入MPW多边形ROI模式");
                await Click(hwnd,"Studio.ModuleCenter.SelectRoi");
                Require(viewer.ToolMode==MVIImageToolMode.MoveOverlay,
                    "真实Studio未能进入MPW选择/移动ROI模式");
                await Click(hwnd,"Studio.ModuleCenter.NavigateRoi");
                Require(viewer.ToolMode==MVIImageToolMode.Navigate &&
                    vm.ProjectSession.Revision==revision,
                    "真实Studio ROI工具切换应恢复导航且不写入MEM");
                await Click(hwnd,"Studio.ModuleCenter.DrawRectangleRoi");
                Trace(report,"原生Win32系统鼠标准备在现有MPW原图拖框");
                Guid newlyDrawnRoi=await DrawRectangleWithNativeMouse(window,viewer,roi.Id);
                Trace(report,"Win32系统鼠标已创建新ROI，尚未写MEM");
                Require(vm.ProjectSession.Revision==revision,
                    "真实鼠标绘制仅应改变MVI临时Overlay，不能写入MEM Revision");
                await Click(hwnd,"Studio.ModuleCenter.NavigateRoi");
                // 旧工程ROI重绑后，无需先执行MRK也无需重读MHS，即可显式
                // 将已选择的有效ROI几何通过原MEM单事务再次提交。
                await Click(hwnd,"Studio.ModuleCenter.CommitSelectedRoi");
                Require(details.Text.Contains("已提交正式MEM Data/Visual") &&
                    vm.ProjectSession.Revision==revision+1 &&
                    vm.ProjectSession.IsDirty,
                    "真实Studio ROI保存按钮必须在零Run状态复用已存配方提交MEM");
                revision=vm.ProjectSession.Revision;
                // 使用原MEM History验证一笔Data+Visual事务能原样撤销/重做，
                // 不由MVI OverlayStore另建编辑历史。
                Trace(report,"新ROI已提交MEM，正在从Studio原Ribbon点击撤销/重做");
                await Click(hwnd,"Studio.Engineering.Undo");
                MEMPage undoPage=vm.ProjectSession.Find(pageId) as MEMPage;
                Require(undoPage!=null &&
                    CxVisionRoiEngineering.Open(vm.ProjectSession,
                        vm.ProjectSession.GetAddress(undoPage.RootContent)).RoiId==roi.Id,
                    "撤销必须原子恢复原ROI的Data与Visual身份");
                await Click(hwnd,"Studio.Engineering.Redo");
                MEMPage redoPage=vm.ProjectSession.Find(pageId) as MEMPage;
                Require(redoPage!=null &&
                    CxVisionRoiEngineering.Open(vm.ProjectSession,
                        vm.ProjectSession.GetAddress(redoPage.RootContent)).RoiId==newlyDrawnRoi &&
                    vm.ProjectSession.Revision>revision,
                    "重做必须原子恢复新ROI Data/Visual且产生有效Revision");
                revision=vm.ProjectSession.Revision;
                vm.SaveProject(project);
                using(MEMEngineeringSession saved=MEMEngineering.Open(project))
                {
                    MEMPage fromDisk=saved.Find(pageId) as MEMPage;
                    Require(fromDisk!=null,"已保存工程中ROI Page身份丢失");
                    CxVisionRoiRecipe recipe=CxVisionRoiEngineering.Open(saved,
                        saved.GetAddress(fromDisk.RootContent));
                    Require(recipe.RoiId==newlyDrawnRoi &&
                        recipe.Roi.Kind==MVIImageOverlayKind.RectangleRoi &&
                        recipe.ConfigurationRevision==43 &&
                        recipe.SourceId=="img05/native/file" &&
                        !saved.IsDirty,
                        "ROI几何编辑不能重写已保存SourceId/MHS配置版本或丢失MEM身份");
                    saved.Close(true);
                }
                Trace(report,"MEM Undo/Redo与保存重开完成，即将点击MRK复算");
                await Click(hwnd,"Studio.ModuleCenter.RunSavedRoi");
                await Until(()=>details.Text.Contains("工程ROI复算完成")||
                    details.Text.Contains("失败"),"点击运行工程ROI");
                Require(details.Text.Contains("工程ROI复算完成") &&
                    results.Items.Count>=4 && viewer.Overlays.Items.Count>0 &&
                    vm.ProjectSession.Revision==revision,
                    "WPF运行未发布真实MRK结果："+details.Text);
                string firstRun=details.Text;
                Trace(report,"MRK Run成功，正在原生SaveFileDialog导出CSV");
                await ClickWithNativeDialog(hwnd,"Studio.ModuleCenter.ExportRunCsv",
                    "导出本次视觉Run",csv);
                Require(File.Exists(csv) && File.ReadAllText(csv).Contains("AcceptedRegionPixelArea"),
                    "系统保存对话框未得到真实Run结构化CSV");
                // 通过UIA真正选择Mask结果项，而不直接调用专业Helper投影。
                await Task.Run(()=>{
                    AutomationElement list=Find(AutomationElement.FromHandle(hwnd),
                        "Studio.ModuleCenter.ResultItems");
                    AutomationElementCollection entries=list.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty,
                            ControlType.ListItem));
                    AutomationElement selected=null;
                    foreach(AutomationElement entry in entries)
                        if(entry.Current.Name.StartsWith("Mask",StringComparison.Ordinal))
                        {selected=entry;break;}
                    Require(selected!=null,"当前Run没有可选择的Mask UIA结果项");
                    SelectionItemPattern item=selected.GetCurrentPattern(
                        SelectionItemPattern.Pattern) as SelectionItemPattern;
                    Require(item!=null,"Mask结果项不支持实际UIA SelectionItem");
                    item.Select();
                });
                await Until(()=>viewer.Session.Result!=null &&
                    details.Text.Contains("Mask"),"UIA选择Mask后显示原Run产物");
                FIMImageBuffer mask=viewer.Session.Result;
                Require(mask.Width==4 && mask.Height==4,
                    "选中Mask后WPF图窗没有显示同尺寸的真实像素");
                // Mask/Result只是只读Run证据，不能把屏幕上残存的ROI
                // 当成用户在原图选择的新工程定义误提交。
                await Click(hwnd,"Studio.ModuleCenter.CommitSelectedRoi");
                Require(details.Text.Contains("Mask/Result是本Run只读证据") &&
                    viewer.Session.ActiveSlot==MVIImageSlot.Result &&
                    vm.ProjectSession.Revision==revision,
                    "在Mask结果槽保存ROI必须拒绝且不变更MEM原配方");
                Trace(report,"Mask只读视图拒绝ROI提交，正在原生SaveFileDialog导出RAW");
                await ClickWithNativeDialog(hwnd,"Studio.ModuleCenter.ExportRunRaw",
                    "导出本Run的无损原始像素",rawExport);
                Require(File.Exists(rawExport) &&
                    new FileInfo(rawExport).Length==(long)mask.Stride*mask.Height,
                    "真实系统保存弹窗未导出被选中的Mask无损RAW");
                await Click(hwnd,"Studio.ModuleCenter.RunSavedRoi");
                await Until(()=>details.Text.Contains("工程ROI复算完成") &&
                    !String.Equals(firstRun,details.Text,StringComparison.Ordinal),
                    "重复按钮Run应形成独立MRK运行身份");
                Require(results.Items.Count>=4 &&
                    vm.ProjectSession.Revision==revision,
                    "第二次真实工程复算丢失产物或修改了工程定义");
                string resultBeforeRejectedBind=details.Text;
                Trace(report,"重复MRK Run成功，开始错误原图RAW拒绝反证");
                await ClickWithNativeDialog(hwnd,"Studio.ModuleCenter.BindSavedRoiRaw",
                    "重新绑定工程ROI原图",wrong);
                await Until(()=>status.Text.Contains("ROI原图重绑失败"),
                    "像素改变的RAW必须拒绝");
                Require(String.Equals(details.Text,resultBeforeRejectedBind,
                    StringComparison.Ordinal) &&
                    results.Items.Count>=4 &&
                    vm.ProjectSession.Revision==revision &&
                    viewer.Session.Original.GetGrayValue(0,0)==210,
                    "错源文件重绑覆盖了有效结果或改变MEM工程状态："+status.Text);
                // 模拟外部SDK在公开MVI Session对同一个Buffer实例重新标记原图。
                // 引用未变但Generation已变化；本Run按钮不得继续开启Save弹框。
                FIMImageBuffer currentSource=viewer.Session.Original;
                long generation=viewer.Session.OriginalGeneration;
                viewer.Session.SetOriginal(currentSource);
                Require(viewer.Session.OriginalGeneration>generation,
                    "公开MVI Session同实例原图重绑定没有递增Generation");
                await Click(hwnd,"Studio.ModuleCenter.ExportRunCsv");
                Require(details.Text.Contains("本Run已失效") &&
                    !File.Exists(Path.Combine(folder,"stale.csv")) &&
                    vm.ProjectSession.Revision==revision,
                    "来源代次已变时UIA导出按钮不得打开文件框或发布旧Run");
                await Click(hwnd,"Studio.ModuleCenter.RunSavedRoi");
                Require(details.Text.Contains("原图来源失效") &&
                    vm.ProjectSession.Revision==revision,
                    "来源代次已变时用户重新点击工程ROI不得发布旧Run");
                // 恢复可信RAW后再次MRK复算，实际切换Mask/Original再显式提交：
                // 新Revision必须立即释放旧Run像素，不能只禁用Export按钮。
                Trace(report,"重新可信RAW重绑与MRK复算，检查旧Result像素释放");
                await ClickWithNativeDialog(hwnd,"Studio.ModuleCenter.BindSavedRoiRaw",
                    "重新绑定工程ROI原图",raw);
                await Until(()=>status.Text.Contains("原图校验通过") ||
                    status.Text.Contains("失败"),"恢复可信ROI原图");
                Require(status.Text.Contains("原图校验通过"),
                    "可信源无法重新恢复："+status.Text);
                await Click(hwnd,"Studio.ModuleCenter.RunSavedRoi");
                await Until(()=>details.Text.Contains("工程ROI复算完成") ||
                    details.Text.Contains("失败"),"恢复可信源MRK再次运行");
                Require(details.Text.Contains("工程ROI复算完成") &&
                    viewer.SelectedOverlayId==newlyDrawnRoi &&
                    results.Items.Count>=4,
                    "MRK重跑后必须从MEM正式配方恢复并选中同一面积ROI");
                await Task.Run(()=>{
                    AutomationElement list=Find(AutomationElement.FromHandle(hwnd),
                        "Studio.ModuleCenter.ResultItems");
                    AutomationElementCollection entries=list.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty,
                            ControlType.ListItem));
                    AutomationElement selected=null;
                    foreach(AutomationElement entry in entries)
                        if(entry.Current.Name.StartsWith("Mask",StringComparison.Ordinal))
                        {selected=entry;break;}
                    Require(selected!=null,"恢复Run的Mask结果不可见");
                    SelectionItemPattern item=selected.GetCurrentPattern(
                        SelectionItemPattern.Pattern) as SelectionItemPattern;
                    Require(item!=null,"恢复Run的Mask不支持UIA选择");
                    item.Select();
                });
                await Until(()=>viewer.Session.Result!=null &&
                    viewer.Session.ActiveSlot==MVIImageSlot.Result,"Mask应显示真实Result");
                await Click(hwnd,"Studio.ModuleCenter.ShowRunOriginal");
                Require(viewer.Session.ActiveSlot==MVIImageSlot.Original &&
                    viewer.Session.Result!=null &&
                    viewer.SelectedOverlayId==newlyDrawnRoi,
                    "从Mask切回原图应保留ROI选择且仍持有尚未失效的Run结果");
                int revisionBeforeCommit=vm.ProjectSession.Revision;
                await Click(hwnd,"Studio.ModuleCenter.CommitSelectedRoi");
                Require(vm.ProjectSession.Revision==revisionBeforeCommit+1 &&
                    viewer.Session.Result==null &&
                    viewer.SelectedOverlayId==newlyDrawnRoi &&
                    viewer.Overlays.Find(newlyDrawnRoi)!=null &&
                    results.Items.Count==0 &&
                    details.Text.Contains("旧Run已失效"),
                    "ROI新Revision须清除旧Run像素/结果列表而保留当前正式ROI");
                Trace(report,"Run像素已随MEM新Revision释放，ROI几何仍在当前图窗");
                // 另一工程编辑者改变了当前MEM Revision。即便原像素Buffer、
                // Page Id和MVI原图代次都不变，旧Overlay也不得覆盖新工程。
                int savedRevision=vm.ProjectSession.Revision;
                MEMPage currentPage=vm.ProjectSession.Find(pageId) as MEMPage;
                Require(currentPage!=null,"最新MEM工程页面丢失");
                vm.ProjectSession.Rename(currentPage,"IMG05外部工程修改后的页面");
                Require(vm.ProjectSession.Revision>savedRevision,
                    "外部MEM真实修改没有推进工程Revision");
                int externalRevision=vm.ProjectSession.Revision;
                await Click(hwnd,"Studio.ModuleCenter.CommitSelectedRoi");
                Require(details.Text.Contains("工程Revision已变化") &&
                    vm.ProjectSession.Revision==externalRevision &&
                    CxVisionRoiEngineering.Open(vm.ProjectSession,
                        vm.ProjectSession.GetAddress(
                            ((MEMPage)vm.ProjectSession.Find(pageId)).RootContent))
                        .RoiId==newlyDrawnRoi,
                    "旧图元绑定必须拒绝覆盖外部已更新的MEM工程定义");
                Trace(report,"外部MEM Revision改变时真实保存按钮拒绝旧Overlay覆盖");
                File.AppendAllText(report,
                    "PASS: IMG05 Studio真实WPF按钮/Windows Open Save文件对话框/RAW重绑/MRK复算/CSV与错源拒绝"+Environment.NewLine+
                    "NOTE: 初始MEM ROI由夹具创建；随后原生Win32鼠标拖框新ROI、实际点击Studio Undo/Redo。非人工或工业HIL。"+Environment.NewLine,
                    new UTF8Encoding(false));
                return true;
            }
            catch(Exception error)
            {
                File.AppendAllText(report,"FAIL: "+error+Environment.NewLine,
                    new UTF8Encoding(false));
                return false;
            }
            finally
            {
                try{Directory.Delete(folder,true);}catch(IOException){}
            }
        }
    }
}
