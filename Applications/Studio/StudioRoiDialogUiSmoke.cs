using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private static AutomationElement FindNativeDialog(string title)
        {
            AutomationElementCollection windows=AutomationElement.RootElement.FindAll(
                TreeScope.Children,new AndCondition(
                    new PropertyCondition(AutomationElement.ProcessIdProperty,
                        Process.GetCurrentProcess().Id),
                    new PropertyCondition(AutomationElement.ControlTypeProperty,
                        ControlType.Window)));
            foreach(AutomationElement window in windows)
            {
                string name=window.Current.Name;
                if(name!=null && name.IndexOf(title,StringComparison.Ordinal)>=0)
                    return window;
            }
            return null;
        }

        private static AutomationElement FindFileNameEditor(AutomationElement dialog)
        {
            AutomationElementCollection controls=dialog.FindAll(TreeScope.Descendants,
                Condition.TrueCondition);
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
                Require(dialog!=null,"系统文件对话框未出现："+title);
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
                throw new TimeoutException("IMG05_UIA 原生文件对话框操作超时："+title);
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

        public static async Task<bool> RunAsync(MainWindow window,string report)
        {
            string folder=Path.Combine(Path.GetTempPath(),
                "CxVisual-IMG05-NativeUIA-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string raw=Path.Combine(folder,"source-4x4-stride10.raw");
                string wrong=Path.Combine(folder,"wrong-pixel.raw");
                string project=Path.Combine(folder,"real-roi.cxproj");
                string csv=Path.Combine(folder,"run-evidence.csv");
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
                    "Studio.ModuleCenter.BindSavedRoiStride","10"));
                await ClickWithNativeDialog(hwnd,"Studio.ModuleCenter.BindSavedRoiRaw",
                    "重新绑定工程ROI原图",raw);
                await Until(()=>status.Text.Contains("原图校验通过")||
                    status.Text.Contains("失败"),"实际RAW文件重绑");
                Require(status.Text.Contains("原图校验通过") &&
                    Object.ReferenceEquals(viewer.Session.Original,null)==false &&
                    viewer.Session.Original.Stride==10 &&
                    vm.ProjectSession.Revision==revision,
                    "真实文件弹框重绑原像素/Stride失败或更改了工程Revision："+status.Text);
                await Click(hwnd,"Studio.ModuleCenter.RunSavedRoi");
                await Until(()=>details.Text.Contains("工程ROI复算完成")||
                    details.Text.Contains("失败"),"点击运行工程ROI");
                Require(details.Text.Contains("工程ROI复算完成") &&
                    results.Items.Count>=4 && viewer.Overlays.Items.Count>0 &&
                    vm.ProjectSession.Revision==revision,
                    "WPF运行未发布真实MRK结果："+details.Text);
                string expectedRun=details.Text;
                await ClickWithNativeDialog(hwnd,"Studio.ModuleCenter.ExportRunCsv",
                    "导出本次视觉Run",csv);
                Require(File.Exists(csv) && File.ReadAllText(csv).Contains("AcceptedRegionPixelArea"),
                    "系统保存对话框未得到真实Run结构化CSV");
                await ClickWithNativeDialog(hwnd,"Studio.ModuleCenter.BindSavedRoiRaw",
                    "重新绑定工程ROI原图",wrong);
                await Until(()=>status.Text.Contains("ROI原图重绑失败"),
                    "像素改变的RAW必须拒绝");
                Require(details.Text.Contains("已导出Run") &&
                    results.Items.Count>=4 &&
                    vm.ProjectSession.Revision==revision &&
                    viewer.Session.Original.GetGrayValue(0,0)==210,
                    "错源文件重绑覆盖了有效结果或改变MEM工程状态："+status.Text);
                File.WriteAllText(report,
                    "PASS: IMG05 Studio真实WPF按钮/Windows Open Save文件对话框/RAW重绑/MRK复算/CSV与错源拒绝\n"+
                    "NOTE: 已有MEM ROI测试夹具；不冒充鼠标画ROI、硬件G6或人工桌面复核。\n",
                    new UTF8Encoding(false));
                return true;
            }
            catch(Exception error)
            {
                File.WriteAllText(report,"FAIL: "+error+Environment.NewLine,
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
