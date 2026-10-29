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
using CxVisual.Professional.Simulation.Modules;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Actual WPF UIA acceptance of formal Simulation FCM/MHS + PSM clock/model +
    /// MRK step, fault refusal, reset/recovery. Never claims physical axis HIL.
    /// </summary>
    public static class TCTSimulationStepUiSmoke
    {
        private static void Check(bool condition,string message)
        {if(!condition)throw new InvalidOperationException(message);}
        private static AutomationElement Element(IntPtr hwnd,string id)
        {
            AutomationElement node=AutomationElement.FromHandle(hwnd).FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty,id));
            if(node==null)throw new InvalidOperationException("UIA element missing: "+id);
            return node;
        }
        private static Task Invoke(IntPtr hwnd,string id)
        {
            return Task.Run(()=>{
                var pattern=Element(hwnd,id).GetCurrentPattern(InvokePattern.Pattern)
                    as InvokePattern;
                if(pattern==null)throw new InvalidOperationException("Missing InvokePattern: "+id);
                pattern.Invoke();
            });
        }
        private static Task Set(IntPtr hwnd,string id,string value)
        {
            return Task.Run(()=>{
                var pattern=Element(hwnd,id).GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                if(pattern==null)throw new InvalidOperationException("Missing ValuePattern: "+id);
                pattern.SetValue(value);
            });
        }
        private static async Task Until(Func<bool> check,string error)
        {
            for(int i=0;i<180;i++)
            {
                if(check())return;
                await Task.Delay(50);
            }
            throw new TimeoutException("UIA check timed out: "+error);
        }
        public static async Task RunAsync()
        {
            string root=Path.Combine(Path.GetTempPath(),"CxVisual-Simulation-UIA-"+
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Window host=null;
            try
            {
                var service=new MHSConfigService(Path.Combine(root,"Configs"));
                string id="Sim"+Guid.NewGuid().ToString("N").Substring(0,10);
                var config=service.ForScope("module",id)
                    .Open<PSMSimulationModuleConfiguration>();
                var commit=await config.UpdateAsync(model=>{
                    model.DisplayName="真实MRK虚拟轴UIA";
                    model.ModelId="axis";
                    model.ProviderTypeId=PSMAxisLinearModel.ProviderId;
                    model.Mode=PSMSimulationRunMode.Step;
                    model.StepMilliseconds=10;
                    model.Seed=42;
                    model.CapabilityIds.Clear();
                    model.CapabilityIds.Add(PSMAxisLinearModel.StepCapabilityId);
                },"TestCenter Simulation UIA正式配置",CancellationToken.None);
                Check(commit.Success,"Simulation FCM保存失败："+commit.ErrorCode);
                if(config.HasPendingApply)
                    Check(config.ApplyPendingAtTaskBoundary(),"Simulation Effective未生效");
                var frame=new TCTManualModuleFrame(Path.Combine(root,"Configs"));
                host=new Window {Title="TestCenter Simulation UIA",Width=1200,Height=820,
                    Content=frame};
                host.Show();host.UpdateLayout();
                frame.SelectModuleKind(MFMModuleKind.Simulation);
                frame.SetModuleId(id);
                IntPtr hwnd=new WindowInteropHelper(host).Handle;
                Check(hwnd!=IntPtr.Zero,"TestCenter WPF window has no HWND");
                await Invoke(hwnd,"TCT.Module.Open");
                await Until(()=>frame.HasOpenedConfiguration&&!frame.IsBusy&&
                    frame.ActiveModuleKind==MFMModuleKind.Simulation,
                    "formal Simulation config open");
                await Task.Run(()=>{
                    var tab=Element(hwnd,"TCT.Module.Page.simulation-step");
                    var pattern=tab.GetCurrentPattern(SelectionItemPattern.Pattern)
                        as SelectionItemPattern;
                    if(pattern==null)throw new InvalidOperationException("Simulation tab not selectable");
                    pattern.Select();
                });
                host.UpdateLayout();
                var panel=frame.SimulationTestPanel;
                Check(panel!=null,"Missing shared Simulation professional UI");
                await Invoke(hwnd,"TCT.Sim.Step");
                await Until(()=>panel.StatusMessage.StartsWith("MRK单步拒绝"),
                    "pre-initialization step refusal");
                Check(panel.CompletedSteps==0,"Pre-initialization caused an advance");
                await Set(hwnd,"TCT.Sim.Velocity","5");
                await Set(hwnd,"TCT.Sim.Limit","100");
                await Invoke(hwnd,"TCT.Sim.Load");
                await Until(()=>panel.StatusMessage.StartsWith("已装配正式Effective"),
                    "formal Simulation MHS Effective load");
                await Invoke(hwnd,"TCT.Sim.Initialize");
                await Until(()=>panel.StatusMessage.StartsWith("模型已用正式Seed初始化"),
                    "PSM virtual model initialization");
                await Invoke(hwnd,"TCT.Sim.Step");
                await Until(()=>panel.CompletedSteps==1&&!panel.IsBusy,
                    "first real MRK virtual step");
                Guid first=panel.LastFlowId;
                Check(first!=Guid.Empty&&Math.Abs(panel.PositionMillimeters-0.05)<0.0000001&&
                    panel.SimulatedTime.TotalMilliseconds==10,
                    "First deterministic model/time step did not execute");
                await Invoke(hwnd,"TCT.Sim.Step");
                await Until(()=>panel.CompletedSteps==2&&!panel.IsBusy,
                    "second real MRK virtual step");
                Guid second=panel.LastFlowId;
                Check(second!=first&&second!=Guid.Empty&&
                    Math.Abs(panel.PositionMillimeters-0.10)<0.0000001&&
                    panel.SimulatedTime.TotalMilliseconds==20,
                    "Second MRK step must advance clock and model consistently");
                await Invoke(hwnd,"TCT.Sim.Fault");
                await Until(()=>panel.StatusMessage.StartsWith("已注入虚拟轴故障"),
                    "inject virtual fault");
                await Invoke(hwnd,"TCT.Sim.Step");
                await Until(()=>panel.StatusMessage.StartsWith("MRK单步拒绝")&&!panel.IsBusy,
                    "MRK failure on injected virtual fault");
                Check(panel.CompletedSteps==2 &&
                    Math.Abs(panel.PositionMillimeters-0.10)<0.0000001&&
                    panel.SimulatedTime.TotalMilliseconds==20,
                    "Faulty MRK step falsely advanced model/clock");
                await Invoke(hwnd,"TCT.Sim.Reset");
                await Until(()=>panel.StatusMessage.StartsWith("专业仿真模块已Reset"),
                    "professional PSM reset");
                Check(panel.SimulatedTime==TimeSpan.Zero&&
                    panel.PositionMillimeters==0&&panel.CompletedSteps==0,
                    "Reset did not clear real model/time state");
                await Invoke(hwnd,"TCT.Sim.Initialize");
                await Invoke(hwnd,"TCT.Sim.Step");
                await Until(()=>panel.CompletedSteps==1&&!panel.IsBusy,
                    "MRK step after recovery");
                Check(panel.LastFlowId!=first&&panel.LastFlowId!=second&&
                    panel.ObservationCount==1&&
                    Math.Abs(panel.PositionMillimeters-0.05)<0.0000001,
                    "Recovery did not reinitialize and execute a fresh MRK Flow");
                var grid=Element(hwnd,"TCT.Sim.Observations")
                    .GetCurrentPattern(GridPattern.Pattern) as GridPattern;
                Check(grid!=null&&grid.Current.RowCount>=1,
                    "Real Simulation MRK observation rows not visible to UIA");
                string report="PASS TCT.Simulation.UIA MHS/PSM/MRK/step/fault/reset/recovery"+
                    Environment.NewLine+"FirstFlowId="+first+
                    Environment.NewLine+"SecondFlowId="+second+
                    Environment.NewLine+"RecoveryFlowId="+panel.LastFlowId+
                    Environment.NewLine+"Effective="+config.EffectiveVersion+
                    Environment.NewLine+"PositionMm="+panel.PositionMillimeters+
                    Environment.NewLine+"SimulatedMs="+panel.SimulatedTime.TotalMilliseconds;
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "TestCenter-Simulation-UIA-Report.txt"),report);
            }
            finally
            {
                if(host!=null)host.Close();
                try{Directory.Delete(root,true);}catch(IOException){}
            }
        }
    }
}
