using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.TestCenter.UI
{
    /// <summary>G1 UIA: actual MCN Connector + FTR Virtual + MPA LengthFrame,
    /// software peer, CRC round-trip, dropped response timeout and restart.</summary>
    public static class TCTConnectorLoopUiSmoke
    {
        private static void Check(bool ok,string reason)
        {if(!ok)throw new InvalidOperationException(reason);}
        private static AutomationElement Find(IntPtr hwnd,string id)
        {
            var node=AutomationElement.FromHandle(hwnd).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty,id));
            if(node==null)throw new InvalidOperationException("Missing Connector UIA "+id);
            return node;
        }
        private static Task Click(IntPtr hwnd,string id)
        {
            return Task.Run(()=>{
                var pattern=Find(hwnd,id).GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                if(pattern==null)throw new InvalidOperationException("UIA Invoke not available "+id);
                pattern.Invoke();
            });
        }
        private static Task Input(IntPtr hwnd,string id,string value)
        {
            return Task.Run(()=>{
                var pattern=Find(hwnd,id).GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                if(pattern==null)throw new InvalidOperationException("UIA Value not available "+id);
                pattern.SetValue(value);
            });
        }
        private static Task Toggle(IntPtr hwnd,string id)
        {
            return Task.Run(()=>{
                var pattern=Find(hwnd,id).GetCurrentPattern(TogglePattern.Pattern) as TogglePattern;
                if(pattern==null)throw new InvalidOperationException("UIA Toggle not available "+id);
                pattern.Toggle();
            });
        }
        private static async Task Until(Func<bool> ok,string reason)
        {
            for(int i=0;i<200;i++)
            {if(ok())return;await Task.Delay(50);}
            throw new TimeoutException("Connector WPF UIA wait: "+reason);
        }
        public static async Task RunAsync()
        {
            string root=Path.Combine(Path.GetTempPath(),"CxVisual-Connector-UIA-"+
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Window window=null;
            try
            {
                string moduleId="Cnn"+Guid.NewGuid().ToString("N").Substring(0,9);
                string configRoot=Path.Combine(root,"Configs");
                var service=new MHSConfigService(configRoot);
                var section=service.ForScope("module",moduleId).Open<MCNConnectorConfiguration>();
                var committed=await section.UpdateAsync(model=>{
                    model.DisplayName="Connector真实LengthFrame UIA";
                    model.Channel=new FTRVirtualChannelConfiguration {
                        Id="tctvirtual"+Guid.NewGuid().ToString("N").Substring(0,8)};
                    model.Protocol=new MPAProtocolConfiguration("tct-length","length.frame");
                    model.Protocol.TimeoutMs=150;
                },"TestCenter Connector Channel/Protocol正式配置",CancellationToken.None);
                Check(committed.Success,"Connector MHS保存失败："+committed.ErrorCode);
                if(section.HasPendingApply)
                    Check(section.ApplyPendingAtTaskBoundary(),"Connector Effective未生效");
                var frame=new TCTManualModuleFrame(configRoot);
                window=new Window {Title="TestCenter Connector WPF UIA",Width=1200,Height=840,
                    Content=frame};
                window.Show();window.UpdateLayout();
                frame.SelectModuleKind(MFMModuleKind.Connector);
                frame.SetModuleId(moduleId);
                IntPtr hwnd=new WindowInteropHelper(window).Handle;
                Check(hwnd!=IntPtr.Zero,"WPF Connector HWND missing");
                await Click(hwnd,"TCT.Module.Open");
                await Until(()=>frame.HasOpenedConfiguration&&!frame.IsBusy&&
                    frame.ActiveModuleKind==MFMModuleKind.Connector,"formal Connector open");
                await Task.Run(()=>{
                    var tab=Find(hwnd,"TCT.Module.Page.connector-echo");
                    var pattern=tab.GetCurrentPattern(SelectionItemPattern.Pattern)
                        as SelectionItemPattern;
                    if(pattern==null)throw new InvalidOperationException("Connector tab UIA missing");
                    pattern.Select();
                });
                window.UpdateLayout();
                var panel=frame.ConnectorTestPanel;
                Check(panel!=null,"Connector module center panel not found");
                await Click(hwnd,"TCT.Connector.Send");
                await Until(()=>panel.StatusMessage.StartsWith("Connector收发拒绝"),
                    "send before Start must be rejected");
                Check(panel.SuccessfulResponses==0,"Unstarted Connector produced fake result");
                await Click(hwnd,"TCT.Connector.Load");
                await Until(()=>panel.StatusMessage.StartsWith("已装配正式Connector Effective"),
                    "one Channel/Protocol Effective assembly");
                await Click(hwnd,"TCT.Connector.Start");
                await Until(()=>panel.IsStarted&&!panel.IsBusy,
                    "real FTR virtual Channel Open");
                await Input(hwnd,"TCT.Connector.Payload","real-ftr-mpa");
                await Click(hwnd,"TCT.Connector.Send");
                await Until(()=>panel.SuccessfulResponses==1&&!panel.IsBusy,
                    "real length-frame payload roundtrip");
                Check(panel.LastPayload=="real-ftr-mpa"&&
                    panel.StatusMessage.Contains("Channel+Protocol回环成功"),
                    "MCN/FTR/MPA true parser loopback did not complete");
                await Toggle(hwnd,"TCT.Connector.DropReply");
                await Click(hwnd,"TCT.Connector.Send");
                await Until(()=>panel.StatusMessage.StartsWith("Connector收发拒绝")&&!panel.IsBusy,
                    "dropped response actual timeout");
                Check(panel.SuccessfulResponses==1,
                    "Timeout incorrectly counted as completed response");
                await Toggle(hwnd,"TCT.Connector.DropReply");
                await Click(hwnd,"TCT.Connector.Send");
                await Until(()=>panel.SuccessfulResponses==2&&!panel.IsBusy,
                    "response recovered after drop");
                await Click(hwnd,"TCT.Connector.Stop");
                await Until(()=>!panel.IsStarted&&!panel.IsBusy,
                    "MCN Close and virtual peer stop");
                await Click(hwnd,"TCT.Connector.Send");
                await Until(()=>panel.StatusMessage.StartsWith("Connector收发拒绝"),
                    "stopped Channel must reject send");
                Check(panel.SuccessfulResponses==2,"Stopped Channel generated a result");
                await Click(hwnd,"TCT.Connector.Start");
                await Until(()=>panel.IsStarted&&!panel.IsBusy,
                    "Connector restarted in new Session");
                await Click(hwnd,"TCT.Connector.Send");
                await Until(()=>panel.SuccessfulResponses==3&&!panel.IsBusy,
                    "real reconnected FTR/MPA roundtrip");
                await Click(hwnd,"TCT.Connector.Stop");
                await Until(()=>!panel.IsStarted&&!panel.IsBusy,
                    "final Connector close");
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "TestCenter-Connector-UIA-Report.txt"),
                    "PASS TCT.Connector.UIA MHS/MCN/FTR/MPA/real-frame/timeout/restart"+
                    Environment.NewLine+"Effective="+section.EffectiveVersion+
                    Environment.NewLine+"Responses="+panel.SuccessfulResponses+
                    Environment.NewLine+"LastPayload="+panel.LastPayload+
                    Environment.NewLine+"FinalState=Stopped"+
                    Environment.NewLine+"Boundary=VirtualPeerNotDeviceHIL");
            }
            finally
            {
                if(window!=null)window.Close();
                try{Directory.Delete(root,true);}catch(IOException){}
            }
        }
    }
}
