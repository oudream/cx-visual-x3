using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Real Windows UIA exercise of the fail-closed Driver readiness screen.
    /// This can succeed without hardware because it proves rejection and honest
    /// discovery only. It is NEVER driver HIL or device-command completion.
    /// </summary>
    public static class TCTDriverReadinessUiSmoke
    {
        private static void Check(bool success,string message)
        {if(!success)throw new InvalidOperationException(message);}
        private static AutomationElement Find(IntPtr hwnd,string id)
        {
            var root=AutomationElement.FromHandle(hwnd);
            var item=root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty,id));
            if(item==null)throw new InvalidOperationException("UIA元素缺失："+id);
            return item;
        }
        private static Task Click(IntPtr hwnd,string id)
        {
            return Task.Run(()=>{
                var item=Find(hwnd,id);
                var action=item.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if(action==null)throw new InvalidOperationException("没有InvokePattern："+id);
                action.Invoke();
            });
        }
        private static Task Set(IntPtr hwnd,string id,string value)
        {
            return Task.Run(()=>{
                var item=Find(hwnd,id);
                var edit=item.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                if(edit==null)throw new InvalidOperationException("没有ValuePattern："+id);
                edit.SetValue(value);
            });
        }
        private static async Task Until(Func<bool> valid,string name)
        {
            for(int i=0;i<150;i++)
            {
                if(valid())return;
                await Task.Delay(40);
            }
            throw new TimeoutException("Driver UIA等待超时："+name);
        }
        public static async Task RunAsync()
        {
            string root=Path.Combine(Path.GetTempPath(),
                "CxVisual-Driver-UIA-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string configRoot=Path.Combine(root,"Configs");
            string id="Driver"+Guid.NewGuid().ToString("N").Substring(0,10);
            string sdk=Path.Combine(root,"vendor-sdk-not-installed.dll");
            Window window=null;
            try
            {
                var frame=new TCTManualModuleFrame(configRoot);
                window=new Window {Title="CxVisual Driver Evidence UIA",Width=1200,Height=850,
                    Content=frame,WindowStartupLocation=WindowStartupLocation.CenterScreen};
                window.Show();window.UpdateLayout();
                frame.SelectModuleKind(MFMModuleKind.Driver);
                frame.SetModuleId(id);
                IntPtr hwnd=new WindowInteropHelper(window).Handle;
                Check(hwnd!=IntPtr.Zero,"测试窗口未创建HWND");
                await Click(hwnd,"TCT.Module.Open");
                await Until(()=>frame.HasOpenedConfiguration&&!frame.IsBusy,
                    "Driver FCM配置打开");
                await Task.Run(()=>{
                    var tab=Find(hwnd,"TCT.Module.Page.driver-probe");
                    var selected=tab.GetCurrentPattern(SelectionItemPattern.Pattern)
                        as SelectionItemPattern;
                    if(selected==null)throw new InvalidOperationException("Driver分页不可选择");
                    selected.Select();
                });
                window.UpdateLayout();
                var panel=frame.DriverTestPanel;
                Check(panel!=null,"Driver预检页没有实际嵌入共享中心");
                await Click(hwnd,"TCT.Driver.Probe");
                await Until(()=>panel.StatusMessage.StartsWith("Driver预检拒绝"),
                    "不存在FCM Effective时必须拒绝");
                Check(panel.LastProbe==null,"未配置实机却给出成功证据");
                // Use actual MHS to persist/activate a valid *declaration*.
                // It is not a device session or real SDK.
                var service=new MHSConfigService(configRoot);
                var section=service.ForScope("module",id)
                    .Open<MFMDriverModuleConfiguration>();
                var committed=await section.UpdateAsync(x=>{
                    x.Enabled=true;x.DisplayName="真实设备依赖负例";
                    x.DriverTypeId="vendor.sdk.not.installed";
                    x.DeviceRef="COM-CXV-NOT-PRESENT";
                    x.Backend=MFMDriverBackendKind.Sdk;
                },"Driver UIA仅配置声明",CancellationToken.None);
                Check(committed.Success,"Driver真实MHS保存失败："+committed.ErrorCode);
                if(section.HasPendingApply)
                    Check(section.ApplyPendingAtTaskBoundary(),"Driver Effective生效失败");
                await Set(hwnd,"TCT.Driver.SdkFile",sdk);
                await Click(hwnd,"TCT.Driver.Probe");
                await Until(()=>panel.LastProbe!=null,"真实Windows/FCM环境证据");
                var evidence=panel.LastProbe;
                Check(evidence.ConfigAccepted&&
                    evidence.DriverTypeId=="vendor.sdk.not.installed" &&
                    evidence.EffectiveVersion==section.EffectiveVersion &&
                    !evidence.SdkFileObserved&&!evidence.OsPortObserved&&
                    !evidence.ProviderResolved&&!evidence.DeviceReady&&
                    !evidence.PhysicalActionVerified,
                    "Driver环境检查不能将缺失SDK或设备判定为Ready");
                Check(panel.StatusMessage.Contains("DeviceReady=否")&&
                    panel.StatusMessage.Contains("PhysicalCompleted=未验证"),
                    "测试人员看不到物理操作未验证的明确结论");
                bool rows=await Task.Run(()=>{
                    var grid=Find(hwnd,"TCT.Driver.ProbeEvidence")
                        .GetCurrentPattern(GridPattern.Pattern) as GridPattern;
                    return grid!=null&&grid.Current.RowCount>=5;
                });
                Check(rows,"实际UIA未显示FCM/SDK/设备/安全证据行");
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "TestCenter-Driver-UIA-Report.txt"),
                    "PASS Driver.G1.UIA FCM-Effective/OS-discovery/missing-SDK/fail-closed"+
                    Environment.NewLine+"Module="+id+
                    Environment.NewLine+"Effective="+evidence.EffectiveVersion+
                    Environment.NewLine+"SdkPresent="+evidence.SdkFileObserved+
                    Environment.NewLine+"DeviceReady="+evidence.DeviceReady+
                    Environment.NewLine+"PhysicalActionVerified="+evidence.PhysicalActionVerified+
                    Environment.NewLine+"HIL=NOT_RUN");
            }
            finally
            {
                if(window!=null)window.Close();
                try{Directory.Delete(root,true);}catch(IOException){}
            }
        }
    }
}
