using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Simulation.Modules;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Manual G1 virtual-axis test. The professional simulation module owns model/time,
    /// MRK executes each user-requested step; this panel owns neither a scheduler
    /// nor a secondary state store. It cannot imply actual axis/PLC HIL success.
    /// </summary>
    public sealed class TCTSimulationStepPanel : UserControl
    {
        private readonly MHSConfigService _service;
        private readonly Func<bool> _dirty;
        private readonly TextBox _velocity=Text("TCT.Sim.Velocity","5");
        private readonly TextBox _limit=Text("TCT.Sim.Limit","100");
        private readonly TextBlock _status=new TextBlock {TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(4)};
        private readonly Button _load=Button("TCT.Sim.Load","读取正式Effective模型");
        private readonly Button _init=Button("TCT.Sim.Initialize","初始化");
        private readonly Button _step=Button("TCT.Sim.Step","MRK单步推进");
        private readonly Button _fault=Button("TCT.Sim.Fault","注入虚拟故障");
        private readonly Button _reset=Button("TCT.Sim.Reset","复位");
        private readonly DataGrid _observations=new DataGrid {IsReadOnly=true,
            AutoGenerateColumns=true,MaxHeight=170,CanUserAddRows=false};
        private readonly List<Observation> _history=new List<Observation>();
        private PSMAxisLinearModel _model;
        private PSMSimulationModule _module;
        private string _id,_effectiveVersion;
        private bool _busy;
        public bool IsBusy {get{return _busy;}}
        public string StatusMessage {get{return _status.Text;}}
        public double PositionMillimeters {get{return _model==null?0:_model.PositionMillimeters;}}
        public TimeSpan SimulatedTime {get{return _module==null?TimeSpan.Zero:_module.SimulatedTime;}}
        public int CompletedSteps {get;private set;}
        public Guid LastFlowId {get;private set;}
        public int ObservationCount {get{return _history.Count;}}
        private sealed class Observation
        {
            public int Step {get;set;}
            public Guid FlowId {get;set;}
            public double PositionMm {get;set;}
            public double TimeMs {get;set;}
            public string MrkState {get;set;}
        }
        public TCTSimulationStepPanel(MHSConfigService service,Func<bool> hasDraft)
        {
            _service=service??throw new ArgumentNullException("service");
            _dirty=hasDraft??throw new ArgumentNullException("hasDraft");
            AutomationProperties.SetAutomationId(this,"TCT.Sim.Panel");
            AutomationProperties.SetAutomationId(_status,"TCT.Sim.Status");
            AutomationProperties.SetAutomationId(_observations,"TCT.Sim.Observations");
            var stack=new StackPanel {Margin=new Thickness(5)};
            stack.Children.Add(new TextBlock {Text="Simulation · 确定性虚拟轴（不连接真实设备）",
                FontWeight=FontWeights.SemiBold});
            stack.Children.Add(new TextBlock {Text="在上方正式Simulation配置中设置ModelId=axis、"+
                "ProviderTypeId=simulation.axis.linear、CapabilityIds包含simulation.axis.step，"+
                "保存并生效。MRK仅按用户点击推进，不启动第二套定时器。",
                TextWrapping=TextWrapping.Wrap});
            var paramsRow=new WrapPanel();
            paramsRow.Children.Add(new TextBlock {Text="虚拟速度 mm/s",VerticalAlignment=VerticalAlignment.Center});
            paramsRow.Children.Add(_velocity);
            paramsRow.Children.Add(new TextBlock {Text="虚拟正限位 mm",VerticalAlignment=VerticalAlignment.Center});
            paramsRow.Children.Add(_limit);
            stack.Children.Add(paramsRow);
            var buttons=new WrapPanel();
            foreach(Button button in new[]{_load,_init,_step,_fault,_reset})buttons.Children.Add(button);
            stack.Children.Add(buttons);stack.Children.Add(_status);
            stack.Children.Add(new TextBlock {Text="MRK真实单步记录（FlowId、模型位置、虚拟时间）"});
            stack.Children.Add(_observations);
            Content=new ScrollViewer {Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                MaxHeight=470};
            _load.Click+=(s,e)=>LoadEffective();
            _init.Click+=(s,e)=>Initialize();
            _step.Click+=async(s,e)=>await AdvanceAsync();
            _fault.Click+=(s,e)=>InjectFault();
            _reset.Click+=(s,e)=>Reset();
            Bind(null);
        }
        private static Button Button(string id,string title)
        {
            var b=new Button {Content=title,Margin=new Thickness(4),Padding=new Thickness(8,4,8,4)};
            AutomationProperties.SetAutomationId(b,id);return b;
        }
        private static TextBox Text(string id,string initial)
        {
            var b=new TextBox {Width=90,Text=initial,Margin=new Thickness(4)};
            AutomationProperties.SetAutomationId(b,id);return b;
        }
        public void Bind(string id)
        {
            if(_busy)throw new InvalidOperationException("MRK执行中不能切换仿真模型");
            if(_id==id)return;
            _id=id;_module=null;_model=null;_effectiveVersion=null;
            _history.Clear();_observations.ItemsSource=null;
            CompletedSteps=0;LastFlowId=Guid.Empty;
            _status.Text=id==null?"请先打开正式Simulation模块配置":
                "已选择 "+id+"，请先保存并生效FCM，然后显式读取Effective。";
        }
        public void SetRuntimeParameters(double velocity,double limit)
        {
            _velocity.Text=velocity.ToString(CultureInfo.InvariantCulture);
            _limit.Text=limit.ToString(CultureInfo.InvariantCulture);
        }
        private PSMSimulationModuleConfiguration RequireEffective(out string version)
        {
            if(String.IsNullOrWhiteSpace(_id))
                throw new InvalidOperationException("尚未打开正式Simulation配置");
            if(_dirty())throw new InvalidOperationException("正式FCM模块草稿未保存");
            var section=_service.OpenExisting<PSMSimulationModuleConfiguration>(
                new FCMConfigIdentity("CxVisual.Module.Simulation","module",_id));
            var versioned=section.EffectiveVersion;
            PSMSimulationModuleConfiguration config;
            using(var capture=section.CaptureForTask("simulation-manual-step"))
                config=capture.Get();
            config.Validate();
            if(!config.Enabled||config.ModelId!="axis"||
                config.ProviderTypeId!=PSMAxisLinearModel.ProviderId||
                config.CapabilityIds==null||
                !config.CapabilityIds.Contains(PSMAxisLinearModel.StepCapabilityId))
                throw new InvalidOperationException("Effective仿真模块未声明真实axis模型及单步能力");
            if(config.Mode!=PSMSimulationRunMode.Step)
                throw new InvalidOperationException("当前测试页仅实现受控Step模式；非Step不能伪装成功");
            version=versioned;return config;
        }
        public void LoadEffective()
        {
            if(_busy){_status.Text="MRK正在运行";return;}
            _module=null;_model=null;_effectiveVersion=null;
            try
            {
                string version;
                PSMSimulationModuleConfiguration config=RequireEffective(out version);
                double velocity=double.Parse(_velocity.Text,CultureInfo.InvariantCulture);
                double limit=double.Parse(_limit.Text,CultureInfo.InvariantCulture);
                _model=new PSMAxisLinearModel(velocity,limit);
                _module=new PSMSimulationModule(config.Describe(_id,MFMModuleKind.Simulation),
                    config,_model);
                _effectiveVersion=version;
                _history.Clear();_observations.ItemsSource=null;CompletedSteps=0;
                LastFlowId=Guid.Empty;
                _status.Text="已装配正式Effective "+version+
                    " / 虚拟轴axis；尚未Initialize和执行，未触发物理设备。";
            }
            catch(Exception ex){_status.Text="Simulation配置/模型拒绝："+ex.Message;}
        }
        public void Initialize()
        {
            if(_busy){_status.Text="MRK运行中不可重新初始化";return;}
            try
            {
                if(_module==null)throw new InvalidOperationException("先读取正式Effective");
                string version;RequireEffective(out version);
                if(version!=_effectiveVersion)throw new InvalidOperationException("配置版本已变化，重新读取");
                _module.Initialize();
                _history.Clear();_observations.ItemsSource=null;CompletedSteps=0;
                LastFlowId=Guid.Empty;
                _status.Text="模型已用正式Seed初始化；位置=0 mm，虚拟时间=0 ms。";
            }
            catch(Exception ex){_status.Text="初始化拒绝："+ex.Message;}
        }
        public async Task AdvanceAsync()
        {
            if(_busy){_status.Text="前一次MRK单步尚未结束";return;}
            Guid flowId=Guid.Empty;
            _busy=true;_step.IsEnabled=false;_load.IsEnabled=false;
            try
            {
                if(_module==null||_model==null||!_model.Initialized)
                    throw new InvalidOperationException("模型尚未初始化");
                string version;var config=RequireEffective(out version);
                if(version!=_effectiveVersion)throw new InvalidOperationException("Effective配置已改变");
                TimeSpan dt=TimeSpan.FromMilliseconds(config.StepMilliseconds);
                // Real MRK executes the professional model; no UI-owned runtime loop.
                var plan=new MRKExecutionPlan("testcenter.sim.axis.step",
                    new[]{new MRKExecutionStep("axis.advance",new string[0],
                        (context,token)=>{
                            token.ThrowIfCancellationRequested();
                            _module.Advance(dt);
                            return Task.FromResult((object)_model.PositionMillimeters);
                        })});
                var engine=new MRKExecutionEngine(new FCRWorkerOptions(1,4));
                try
                {
                    var handle=engine.StartFlow(plan);
                    MRKFlowResult result=await handle.Completion;
                    flowId=result.FlowId;
                    if(result.State!=MRKFlowState.Completed)
                        throw new InvalidOperationException("MRK拒绝/失败："+result.State+
                            (result.Error==null?"":" / "+result.Error.Message));
                    CompletedSteps++;
                    LastFlowId=result.FlowId;
                    _history.Add(new Observation {Step=CompletedSteps,FlowId=result.FlowId,
                        PositionMm=_model.PositionMillimeters,
                        TimeMs=_module.SimulatedTime.TotalMilliseconds,MrkState=result.State.ToString()});
                    _observations.ItemsSource=_history.ToArray();
                    _status.Text="MRK真实单步Completed；FlowId="+result.FlowId.ToString("N")+
                        "；位置="+_model.PositionMillimeters.ToString("F3",CultureInfo.InvariantCulture)+
                        " mm；虚拟时间="+_module.SimulatedTime.TotalMilliseconds+
                        " ms；Effective="+_effectiveVersion;
                }
                finally{await engine.StopAsync(true);}
            }
            catch(Exception ex)
            {
                _status.Text="MRK单步拒绝："+ex.Message+
                    "；虚拟位置/时间以实际专业模型为准，不能伪报推进。";
            }
            finally{_busy=false;_step.IsEnabled=true;_load.IsEnabled=true;}
        }
        public void InjectFault()
        {
            if(_busy){_status.Text="MRK运行中禁止注入";return;}
            try{if(_model==null)throw new InvalidOperationException("先读取模型");
                _model.InjectFault();_status.Text="已注入虚拟轴故障，下次MRK推进应拒绝。";}
            catch(Exception ex){_status.Text="故障注入拒绝："+ex.Message;}
        }
        public void Reset()
        {
            if(_busy){_status.Text="MRK运行中禁止复位";return;}
            try
            {
                if(_module==null)throw new InvalidOperationException("先读取模型");
                _module.Reset();
                _history.Clear();_observations.ItemsSource=null;
                CompletedSteps=0;LastFlowId=Guid.Empty;
                _status.Text="专业仿真模块已Reset：模型未初始化/虚拟时间归零。";
            }
            catch(Exception ex){_status.Text="复位拒绝："+ex.Message;}
        }
        public void Stop() {if(_busy)throw new InvalidOperationException("MRK执行中，禁止静默释放仿真资源");}
    }
}
