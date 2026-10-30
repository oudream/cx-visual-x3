using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Lifecycle.Modules;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// True TestCenter WPF operator workflow for actual MLC process-local Service.
    /// No HTTP listener, MES integration, mock readiness or secondary runtime host.
    /// </summary>
    public static class TCTServiceRequestUiSmoke
    {
        private static void Check(bool ok,string reason)
        {if(!ok)throw new InvalidOperationException(reason);}
        private static AutomationElement Find(IntPtr hwnd,string id)
        {
            var found=AutomationElement.FromHandle(hwnd).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty,id));
            if(found==null)throw new InvalidOperationException("Missing Service UIA: "+id);
            return found;
        }
        private static Task Invoke(IntPtr hwnd,string id)
        {
            return Task.Run(()=>{
                var p=Find(hwnd,id).GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if(p==null)throw new InvalidOperationException("Missing Invoke: "+id);
                p.Invoke();
            });
        }
        private static Task Set(IntPtr hwnd,string id,string value)
        {
            return Task.Run(()=>{
                var p=Find(hwnd,id).GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                if(p==null)throw new InvalidOperationException("Missing Value: "+id);
                p.SetValue(value);
            });
        }
        private static async Task Until(Func<bool> pred,string description)
        {
            for(int i=0;i<180;i++)
            {if(pred())return;await Task.Delay(50);}
            throw new TimeoutException("Service UIA wait: "+description);
        }
        public static async Task RunAsync()
        {
            string root=Path.Combine(Path.GetTempPath(),"CxVisual-Service-UIA-"+
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Window host=null;
            try
            {
                string id="Srv"+Guid.NewGuid().ToString("N").Substring(0,10);
                string configPath=Path.Combine(root,"Configs");
                var service=new MHSConfigService(configPath);
                var section=service.ForScope("module",id).Open<MLCServiceModuleConfiguration>();
                var committed=await section.UpdateAsync(model=>{
                    model.DisplayName="MLC真实Service UIA";
                    model.Enabled=true;
                    model.ProviderTypeId=TCTLocalEchoService.ProviderId;
                    model.CapabilityIds.Clear();
                    model.CapabilityIds.Add(TCTLocalEchoService.CapabilityId);
                    model.MaxConcurrentRequests=2;
                    model.DependencyRefs.Clear();
                },"TestCenter Service MLC真实UIA",CancellationToken.None);
                Check(committed.Success,"Service MHS FCM提交失败："+committed.ErrorCode);
                if(section.HasPendingApply)
                    Check(section.ApplyPendingAtTaskBoundary(),"Service正式Effective未生效");
                var frame=new TCTManualModuleFrame(configPath);
                host=new Window {Title="TestCenter Service Request WPF UIA",
                    Width=1250,Height=840,Content=frame};
                host.Show();host.UpdateLayout();
                frame.SelectModuleKind(MFMModuleKind.Service);
                frame.SetModuleId(id);
                IntPtr hwnd=new WindowInteropHelper(host).Handle;
                Check(hwnd!=IntPtr.Zero,"No actual WPF HWND");
                await Invoke(hwnd,"TCT.Module.Open");
                await Until(()=>frame.HasOpenedConfiguration&&!frame.IsBusy&&
                    frame.ActiveModuleKind==MFMModuleKind.Service,
                    "official Service module open");
                await Task.Run(()=>{
                    var element=Find(hwnd,"TCT.Module.Page.service-requests");
                    var p=element.GetCurrentPattern(SelectionItemPattern.Pattern)
                        as SelectionItemPattern;
                    if(p==null)throw new InvalidOperationException("Service Tab not selectable");
                    p.Select();
                });
                host.UpdateLayout();
                var panel=frame.ServiceTestPanel;
                Check(panel!=null,"No real Service operation panel");
                await Invoke(hwnd,"TCT.Service.Start");
                await Until(()=>panel.StatusMessage.StartsWith("Service启动拒绝"),
                    "reject Start without Effective");
                Check(panel.State==MLCServiceModuleState.Stopped,
                    "Default Service started without real config");
                await Invoke(hwnd,"TCT.Service.Load");
                await Until(()=>panel.StatusMessage.StartsWith("已装配Service Effective"),
                    "load approved MHS Effective");
                await Invoke(hwnd,"TCT.Service.Start");
                await Until(()=>panel.State==MLCServiceModuleState.Running&&!panel.IsBusy,
                    "actual MLC Ready");
                Check(panel.StatusMessage.Contains("本地请求Ready证据=已核验"),
                    "TestCenter must display real handler readiness, not only Running");
                await Set(hwnd,"TCT.Service.RequestId","req-1");
                await Set(hwnd,"TCT.Service.Payload","hello CxVisual");
                await Invoke(hwnd,"TCT.Service.Send");
                await Until(()=>panel.StatusMessage.StartsWith("真实本地Service响应")&&!panel.IsBusy,
                    "first actual request-response");
                var first=panel.LastResponse;
                Check(first!=null&&first.Output=="HELLO CXVISUAL"&&
                    first.Sequence==1&&first.Sha256.Length==64&&
                    panel.UniqueRequestCount==1,
                    "Service did not compute real response");
                await Invoke(hwnd,"TCT.Service.Send");
                await Until(()=>panel.ObservationCount>=2&&!panel.IsBusy,
                    "same RequestId idempotent retry");
                var duplicate=panel.LastResponse;
                Check(Object.ReferenceEquals(first,duplicate)&&
                    panel.UniqueRequestCount==1&&duplicate.Sequence==1,
                    "Idempotent repeat must retain the same true service response");
                await Set(hwnd,"TCT.Service.Payload","conflict");
                await Invoke(hwnd,"TCT.Service.Send");
                await Until(()=>panel.StatusMessage.StartsWith("Service请求拒绝")&&!panel.IsBusy,
                    "conflicting idempotency key rejected");
                Check(panel.UniqueRequestCount==1,
                    "Conflicting request must not modify service result store");
                await Invoke(hwnd,"TCT.Service.Stop");
                await Until(()=>panel.State==MLCServiceModuleState.Stopped&&!panel.IsBusy,
                    "actual lifecycle Stop");
                await Invoke(hwnd,"TCT.Service.Send");
                await Until(()=>panel.StatusMessage.StartsWith("Service请求拒绝")&&!panel.IsBusy,
                    "reject after Stop");
                await Invoke(hwnd,"TCT.Service.Start");
                await Until(()=>panel.State==MLCServiceModuleState.Running&&!panel.IsBusy,
                    "restart MLC service lifecycle");
                await Set(hwnd,"TCT.Service.RequestId","req-2");
                await Set(hwnd,"TCT.Service.Payload","again");
                await Invoke(hwnd,"TCT.Service.Send");
                await Until(()=>panel.StatusMessage.StartsWith("真实本地Service响应")&&!panel.IsBusy,
                    "actual response after Restart");
                Check(panel.LastResponse.Output=="AGAIN"&&
                    panel.UniqueRequestCount==1&&panel.LastResponse.Sequence==1,
                    "Service restart did not register a new real handler");
                var table=Find(hwnd,"TCT.Service.Responses").GetCurrentPattern(GridPattern.Pattern)
                    as GridPattern;
                Check(table!=null&&table.Current.RowCount>=1,
                    "Real Service response not displayed in WPF grid");
                await Invoke(hwnd,"TCT.Service.Stop");
                await Until(()=>panel.State==MLCServiceModuleState.Stopped&&!panel.IsBusy,
                    "final Stop");
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "TestCenter-Service-UIA-Report.txt"),
                    "PASS TCT.Service.UIA MHS/MLC/start/response/idempotency/reject/restart/stop"+
                    Environment.NewLine+"Effective="+section.EffectiveVersion+
                    Environment.NewLine+"FirstSHA256="+first.Sha256+
                    Environment.NewLine+"FirstSequence="+first.Sequence+
                    Environment.NewLine+"RestartOutput="+panel.LastResponse.Output+
                    Environment.NewLine+"FinalState="+panel.State);
            }
            finally
            {
                if(host!=null)host.Close();
                try{Directory.Delete(root,true);}catch(IOException){}
            }
        }
    }
}
