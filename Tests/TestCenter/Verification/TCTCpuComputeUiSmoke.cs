using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Professional.Vision;
using CxVisual.Professional.Vision.Modules;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Actual visible HWND + UIAutomation ValuePattern/InvokePattern acceptance for
    /// TestCenter's Compute tab. Executes real on-disk RAW -> MHS -> MEM/MOC/MRK,
    /// checks negative cases and repeats with a distinct MRK FlowId.
    /// </summary>
    public static class TCTCpuComputeUiSmoke
    {
        private static void Check(bool yes,string message)
        {if(!yes)throw new InvalidOperationException(message);}
        private static AutomationElement Find(AutomationElement root,string id)
        {
            var item=root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty,id));
            if(item==null)throw new InvalidOperationException("UIA missing: "+id);
            return item;
        }
        private static async Task Click(IntPtr hwnd,string id)
        {
            await Task.Run(()=>{
                var item=Find(AutomationElement.FromHandle(hwnd),id);
                var action=item.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if(action==null)throw new InvalidOperationException("No UIA Invoke: "+id);
                action.Invoke();
            });
        }
        private static async Task Set(IntPtr hwnd,string id,string value)
        {
            await Task.Run(()=>{
                var item=Find(AutomationElement.FromHandle(hwnd),id);
                var edit=item.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                if(edit==null)throw new InvalidOperationException("No UIA Value: "+id);
                edit.SetValue(value);
            });
            // Real WPF ValuePattern inputs are marshalled to the owning dispatcher.
            // Confirm the displayed value is committed before the next click.
            bool applied=false;
            for(int attempt=0;attempt<80&&!applied;attempt++)
            {
                applied=await Task.Run(()=>{
                    var item=Find(AutomationElement.FromHandle(hwnd),id);
                    var edit=item.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                    return edit!=null&&String.Equals(edit.Current.Value,value,StringComparison.Ordinal);
                });
                if(!applied)await Task.Delay(25);
            }
            if(!applied)throw new InvalidOperationException(
                "UIA输入未生效："+id+" expected="+value);
        }
        private static async Task Until(Func<bool> ok,string reason,int loops=250)
        {
            for(int i=0;i<loops;i++)
            {
                if(ok())return;
                await Task.Delay(50);
            }
            throw new TimeoutException("UIA waiting: "+reason);
        }
        public static async Task RunAsync()
        {
            string root=Path.Combine(Path.GetTempPath(),
                "CxVisual-TCT-CPU-UIA-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string configRoot=Path.Combine(root,"Configs");
            string id="Cpu"+Guid.NewGuid().ToString("N").Substring(0,10);
            string raw=Path.Combine(root,"sample.raw");
            File.WriteAllBytes(raw,new byte[]{210,210,0,0,0,210,0,0,0,0,0,0,0,0,250});
            Window host=null;
            try
            {
                // Pre-provision a *real* FCM Compute module through its MHS Owner.
                // The UI test subsequently clicks Save Vision, Load Effective, Run.
                var service=new MHSConfigService(configRoot);
                var module=service.ForScope("module",id)
                    .Open<PVCComputeModuleConfiguration>();
                var saved=await module.UpdateAsync(model=>{
                    model.DisplayName="CPU专业真实UIA";
                    model.Device=PVCComputeDevicePreference.Cpu;
                    model.ProviderTypeId=CxVisionThresholdProvider.CapabilityId;
                    model.CapabilityIds.Clear();
                    model.CapabilityIds.Add(CxVisionThresholdProvider.CapabilityId);
                },"CPU UIA真实模块配置",CancellationToken.None);
                Check(saved.Success,"Compute FCM MHS setup failed: "+saved.ErrorCode);
                if(module.HasPendingApply)
                    Check(module.ApplyPendingAtTaskBoundary(),"Compute Effective not applied");
                var frame=new TCTManualModuleFrame(configRoot);
                host=new Window {Title="TestCenter Compute Real UIA",Width=1300,Height=900,
                    Content=frame,WindowStartupLocation=WindowStartupLocation.CenterScreen};
                host.Show();host.UpdateLayout();
                frame.SelectModuleKind(MFMModuleKind.Compute);
                frame.SetModuleId(id);
                host.UpdateLayout();
                var hwnd=new WindowInteropHelper(host).Handle;
                Check(hwnd!=IntPtr.Zero,"No TestCenter Compute HWND");
                await Click(hwnd,"TCT.Module.Open");
                await Until(()=>frame.HasOpenedConfiguration&&!frame.IsBusy&&
                    frame.ActiveModuleKind==MFMModuleKind.Compute,"open real FCM module");
                await Task.Run(()=>{
                    var page=Find(AutomationElement.FromHandle(hwnd),"TCT.Module.Page.cpu-test");
                    var select=page.GetCurrentPattern(SelectionItemPattern.Pattern)
                        as SelectionItemPattern;
                    if(select==null)throw new InvalidOperationException("Compute page UIA select missing");
                    select.Select();
                });
                host.UpdateLayout();
                var panel=frame.CpuTestPanel;
                Check(panel!=null,"Compute panel not attached to common Module Center");
                // No vision config exists; UI must refuse a read without creating one.
                await Click(hwnd,"TCT.Cpu.Load");
                await Until(()=>panel.StatusMessage.StartsWith("Effective读取拒绝"),
                    "missing Vision Effective rejected");
                Check(panel.LastReport==null,"Missing config was reported as executed");
                await Set(hwnd,"TCT.Cpu.RawPath",raw);
                await Set(hwnd,"TCT.Cpu.Width","5");
                await Set(hwnd,"TCT.Cpu.Height","3");
                await Set(hwnd,"TCT.Cpu.Threshold","200");
                await Set(hwnd,"TCT.Cpu.MinArea","2");
                await Set(hwnd,"TCT.Cpu.MaxArea","100");
                await Click(hwnd,"TCT.Cpu.Save");
                await Until(()=>panel.StatusMessage.StartsWith("Vision已保存并生效"),
                    "real MHS Vision save/apply");
                await Click(hwnd,"TCT.Cpu.Load");
                await Until(()=>panel.StatusMessage.StartsWith("已读取正式Compute/Vision Effective"),
                    "real provider Effective load");
                // Negative: a mismatched width must fail without fake success.
                await Set(hwnd,"TCT.Cpu.Width","6");
                await Click(hwnd,"TCT.Cpu.Run");
                // Async WPF handler publishes failure text before its finally re-enables
                // controls. Never start the valid retry while prior Run still owns _running.
                await Until(()=>!panel.IsRunning &&
                    panel.StatusMessage.StartsWith("CPU检测失败"),
                    "invalid RAW dimensions refused and first Run fully settled");
                Check(panel.LastReport==null,"Invalid file returned fake result");
                await Set(hwnd,"TCT.Cpu.Width","5");
                await Click(hwnd,"TCT.Cpu.Run");
                await Until(()=>!panel.IsRunning&&
                    (panel.LastReport!=null ||
                     panel.StatusMessage.StartsWith("CPU检测失败") ||
                     panel.StatusMessage.StartsWith("CPU检测已取消")),
                    "real CPU inspection completion");
                if(panel.LastReport==null)
                    throw new InvalidOperationException("真实CPU执行未产生结果："+panel.StatusMessage);
                var first=panel.LastReport;
                Check(first.Inspection.ThresholdPixelCount==4 &&
                    first.Inspection.Regions.Count==1&&
                    first.Inspection.Regions[0].PixelArea==3 &&
                    first.AcceptedRegionPixelArea==3 &&
                    first.MeasureBlockId!=Guid.Empty &&
                    first.Evidence.Items.Any(x=>
                        x.Kind==CxVisionResultKind.Measurement && x.IntegerValue==3) &&
                    first.Trace.Any(x=>x.BlockId==first.MeasureBlockId &&
                        x.State=="Completed") &&
                    !first.Inspection.Passed &&first.CompletedTraceEvents>0 &&
                    first.Trace.Count>0 &&first.FlowId!=Guid.Empty &&
                    first.Fingerprint.Length==64,
                    "Real RAW/MHS/MEM/MOC/MRK result/Trace mismatch");
                bool hasResults=await Task.Run(()=>{
                    var rootElement=AutomationElement.FromHandle(hwnd);
                    var regions=Find(rootElement,"TCT.Cpu.Regions");
                    var trace=Find(rootElement,"TCT.Cpu.Trace");
                    var regionGrid=regions.GetCurrentPattern(GridPattern.Pattern) as GridPattern;
                    var traceGrid=trace.GetCurrentPattern(GridPattern.Pattern) as GridPattern;
                    return regionGrid!=null&&traceGrid!=null&&
                        regionGrid.Current.RowCount>=1&&traceGrid.Current.RowCount>=1;
                });
                Check(hasResults,"Actual WPF grids do not contain real region/Trace rows");
                // Repeat through the same UI must produce a new actual MRK execution.
                await Click(hwnd,"TCT.Cpu.Run");
                await Until(()=>!panel.IsRunning&&
                    ((panel.LastReport!=null&&panel.LastReport.FlowId!=first.FlowId) ||
                     panel.StatusMessage.StartsWith("CPU检测失败") ||
                     panel.StatusMessage.StartsWith("CPU检测已取消")),
                    "repeat real execution");
                if(panel.LastReport==null||panel.LastReport.FlowId==first.FlowId)
                    throw new InvalidOperationException("重复CPU执行无新FlowId："+panel.StatusMessage);
                var second=panel.LastReport;
                Check(second.Inspection.Regions.Count==1 &&
                    second.CompletedTraceEvents>0&&second.PageId!=first.PageId,
                    "Repeat did not create distinct MEM/MRK evidence");
                Check(panel.StatusMessage.Contains("FlowId=") &&
                    panel.StatusMessage.Contains("MOC指纹=") &&
                    panel.StatusMessage.Contains("真实Trace="),
                    "Operator cannot inspect actual result provenance");
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "TestCenter-Cpu-UIA-Report.txt"),
                    "PASS TCT.Compute.UIA RAW/MHS Effective/MEM/MOC/MRK/results/Trace/repeat/negative"+
                    Environment.NewLine+"FlowId1="+first.FlowId+
                    Environment.NewLine+"FlowId2="+second.FlowId+
                    Environment.NewLine+"Config="+second.ConfigVersion+
                    Environment.NewLine+"Fingerprint="+second.Fingerprint+
                    Environment.NewLine+"ThresholdPixels="+second.Inspection.ThresholdPixelCount+
                    Environment.NewLine+"Defects="+second.Inspection.Regions.Count);
            }
            finally
            {
                if(host!=null)host.Close();
                try{Directory.Delete(root,true);}catch(IOException){}
            }
        }
    }
}
