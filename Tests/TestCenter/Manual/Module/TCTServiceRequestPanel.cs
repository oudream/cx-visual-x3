using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Lifecycle.Modules;

namespace CxVisual.TestCenter.UI
{
    public sealed class TCTServiceEchoResponse
    {
        public string RequestId {get;internal set;}
        public string Output {get;internal set;}
        public string Sha256 {get;internal set;}
        public int Sequence {get;internal set;}
        public DateTime CompletedUtc {get;internal set;}
    }
    /// <summary>
    /// Real process-local request handler registered/unregistered through the actual
    /// MLC lifecycle. Does not create another Runtime Host or expose an HTTP endpoint.
    /// This is intentionally not MES/ERP integration or a production identity service.
    /// </summary>
    internal sealed class TCTLocalEchoService : MLCServiceModuleBase
    {
        public const string ProviderId="testcenter.service.local.echo";
        public const string CapabilityId="service.local.echo";
        private readonly object _gate=new object();
        private readonly Dictionary<string,TCTServiceEchoResponse> _records=
            new Dictionary<string,TCTServiceEchoResponse>(StringComparer.Ordinal);
        private SemaphoreSlim _slots;
        private int _sequence;
        private bool _accepting;
        public int UniqueRequests {get{lock(_gate)return _records.Count;}}
        // Actual request handler state, not a process-level proxy for Ready.
        protected override bool TryObserveReadiness(out bool ready)
        {
            lock(_gate)
            {
                if(Options.DependencyRefs.Count!=0)
                {
                    ready=false; // Dependency readiness is not observable here.
                    return false;
                }
                ready=_accepting && _slots!=null;
                return true;
            }
        }
        public TCTLocalEchoService(MFMModuleDescriptor descriptor,MLCServiceModuleConfiguration config)
            :base(descriptor,config) {}
        protected override Task OnStartAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock(_gate)
            {
                if(_slots!=null)throw new InvalidOperationException("已有服务接入槽");
                _slots=new SemaphoreSlim(Options.MaxConcurrentRequests,Options.MaxConcurrentRequests);
                _records.Clear();_sequence=0;_accepting=true;
            }
            return Task.CompletedTask;
        }
        protected override Task OnStopAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock(_gate)
            {
                _accepting=false;
                if(_slots!=null)
                {
                    if(_slots.CurrentCount!=Options.MaxConcurrentRequests)
                        throw new InvalidOperationException("服务仍有正在处理的请求，不可标记Stopped");
                    _slots.Dispose();
                    _slots=null;
                }
            }
            return Task.CompletedTask;
        }
        public async Task<TCTServiceEchoResponse> RequestAsync(
            string requestId,string value,CancellationToken token)
        {
            if(String.IsNullOrWhiteSpace(requestId)||requestId.Length>80)
                throw new ArgumentException("RequestId须为1～80字符");
            if(value==null||Encoding.UTF8.GetByteCount(value)>65536)
                throw new ArgumentException("请求负载超过64KB");
            if(!IsReady)throw new InvalidOperationException("服务未Ready，拒绝请求");
            SemaphoreSlim slots;
            lock(_gate)
            {
                if(!_accepting||_slots==null)throw new InvalidOperationException("服务已停止");
                slots=_slots;
            }
            await slots.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                if(!IsReady)throw new InvalidOperationException("服务非Running状态");
                byte[] bytes=Encoding.UTF8.GetBytes(value);
                string digest;
                using(var sha=SHA256.Create())
                    digest=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
                lock(_gate)
                {
                    if(!_accepting)throw new InvalidOperationException("服务已停止");
                    TCTServiceEchoResponse existing;
                    if(_records.TryGetValue(requestId,out existing))
                    {
                        if(existing.Sha256!=digest)
                            throw new InvalidOperationException("相同RequestId对应不同请求内容，幂等冲突");
                        return existing;
                    }
                    var output=new TCTServiceEchoResponse {
                        RequestId=requestId,
                        Output=value.ToUpperInvariant(),
                        Sha256=digest,
                        Sequence=++_sequence,CompletedUtc=DateTime.UtcNow
                    };
                    _records.Add(requestId,output);
                    return output;
                }
            }
            finally{slots.Release();}
        }
    }

    /// <summary>
    /// Real TestCenter Service G1 user actions: MHS Effective → MLC lifecycle →
    /// in-process request/response/idempotency → visible receipt and refusal paths.
    /// </summary>
    public sealed class TCTServiceRequestPanel : UserControl
    {
        private readonly MHSConfigService _service;
        private readonly Func<bool> _dirty;
        private readonly TextBox _requestId=Text("TCT.Service.RequestId","req001",130);
        private readonly TextBox _payload=Text("TCT.Service.Payload","Hello CxVisual",280);
        private readonly TextBlock _status=new TextBlock {Margin=new Thickness(5),
            TextWrapping=TextWrapping.Wrap};
        private readonly Button _load=Button("TCT.Service.Load","读取正式Effective");
        private readonly Button _start=Button("TCT.Service.Start","启动服务");
        private readonly Button _send=Button("TCT.Service.Send","提交真实本地请求");
        private readonly Button _stop=Button("TCT.Service.Stop","停止服务");
        private readonly DataGrid _responses=new DataGrid {MaxHeight=180,
            IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false};
        private readonly List<TCTServiceEchoResponse> _observations=
            new List<TCTServiceEchoResponse>();
        private TCTLocalEchoService _runningService;
        private string _moduleId,_effectiveVersion;
        private bool _busy;
        public bool IsBusy {get{return _busy;}}
        public string StatusMessage {get{return _status.Text;}}
        public int UniqueRequestCount {get{return _runningService==null?0:_runningService.UniqueRequests;}}
        public TCTServiceEchoResponse LastResponse {get;private set;}
        public MLCServiceModuleState State {get{return _runningService==null?
            MLCServiceModuleState.Stopped:_runningService.State;}}
        public int ObservationCount {get{return _observations.Count;}}
        public TCTServiceRequestPanel(MHSConfigService service,Func<bool> hasDraft)
        {
            _service=service??throw new ArgumentNullException("service");
            _dirty=hasDraft??throw new ArgumentNullException("hasDraft");
            AutomationProperties.SetAutomationId(this,"TCT.Service.Panel");
            AutomationProperties.SetAutomationId(_status,"TCT.Service.Status");
            AutomationProperties.SetAutomationId(_responses,"TCT.Service.Responses");
            var root=new StackPanel {Margin=new Thickness(4)};
            root.Children.Add(new TextBlock {Text="Service · 进程内真实请求/响应（非MES/ERP）",
                FontWeight=FontWeights.SemiBold});
            root.Children.Add(new TextBlock {Text="正式Service配置需要Enabled、ProviderTypeId="+
                TCTLocalEchoService.ProviderId+"，CapabilityIds包含"+
                TCTLocalEchoService.CapabilityId+"。本页复用MLC生命周期，不创建新Host或网络监听。",
                TextWrapping=TextWrapping.Wrap});
            var commands=new WrapPanel();
            foreach(var b in new[]{_load,_start,_send,_stop})commands.Children.Add(b);
            root.Children.Add(commands);
            var inputs=new WrapPanel();
            inputs.Children.Add(new TextBlock {Text="RequestId",VerticalAlignment=VerticalAlignment.Center});
            inputs.Children.Add(_requestId);
            inputs.Children.Add(new TextBlock {Text="请求数据",VerticalAlignment=VerticalAlignment.Center});
            inputs.Children.Add(_payload);
            root.Children.Add(inputs);
            root.Children.Add(_status);
            root.Children.Add(new TextBlock {Text="真实响应与幂等记录（当前服务实例）"});
            root.Children.Add(_responses);
            Content=new ScrollViewer {Content=root,MaxHeight=470,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            _load.Click+=(s,e)=>LoadEffective();
            _start.Click+=async(s,e)=>await StartAsync();
            _send.Click+=async(s,e)=>await RequestAsync();
            _stop.Click+=async(s,e)=>await StopAsync();
            Bind(null);
        }
        private static TextBox Text(string id,string value,double width)
        {
            var box=new TextBox {Text=value,Width=width,Margin=new Thickness(4)};
            AutomationProperties.SetAutomationId(box,id);return box;
        }
        private static Button Button(string id,string label)
        {
            var btn=new Button {Content=label,Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(4)};
            AutomationProperties.SetAutomationId(btn,id);return btn;
        }
        public void Bind(string id)
        {
            if(_busy||(_runningService!=null&&_runningService.IsReady))
                throw new InvalidOperationException("服务仍在运行，先停止再切换模块");
            if(_moduleId==id)return;
            _moduleId=id;_runningService=null;_effectiveVersion=null;LastResponse=null;
            _observations.Clear();_responses.ItemsSource=null;
            _status.Text=id==null?"请先打开正式Service配置":
                "已选择Service "+id+"。保存生效FCM后加载正式配置。";
        }
        public void SetRequest(string id,string data)
        {_requestId.Text=id??"";_payload.Text=data??"";}
        private MLCServiceModuleConfiguration RequireEffective(out string version)
        {
            if(String.IsNullOrWhiteSpace(_moduleId))
                throw new InvalidOperationException("未选择Service模块");
            if(_dirty())throw new InvalidOperationException("FCM配置草稿未保存");
            var section=_service.OpenExisting<MLCServiceModuleConfiguration>(
                new FCMConfigIdentity("CxVisual.Module.Service","module",_moduleId));
            MLCServiceModuleConfiguration config;
            using(var capture=section.CaptureForTask("service-local-request"))
                config=capture.Get();
            version=section.EffectiveVersion;
            config.Validate();
            if(!config.Enabled||config.ProviderTypeId!=TCTLocalEchoService.ProviderId||
                config.CapabilityIds==null||
                !config.CapabilityIds.Contains(TCTLocalEchoService.CapabilityId))
                throw new InvalidOperationException("Service Effective未声明支持的真实本地Provider/Capability");
            if(config.DependencyRefs.Count>0)
                throw new InvalidOperationException("本地G1服务不冒充已满足外部Service依赖");
            return config;
        }
        public void LoadEffective()
        {
            if(_busy){_status.Text="请求/生命周期操作中，不可换配置";return;}
            if(_runningService!=null&&_runningService.IsReady)
            {_status.Text="先停止服务，才能更换Effective";return;}
            _runningService=null;_effectiveVersion=null;LastResponse=null;
            _observations.Clear();_responses.ItemsSource=null;
            try
            {
                string version;
                var config=RequireEffective(out version);
                _runningService=new TCTLocalEchoService(config.Describe(_moduleId,MFMModuleKind.Service),config);
                _effectiveVersion=version;
                _status.Text="已装配Service Effective "+version+"；状态Stopped，尚未Ready。";
            }
            catch(Exception ex){_status.Text="Service配置拒绝："+ex.Message;}
        }
        public async Task StartAsync()
        {
            if(_busy){_status.Text="服务操作尚未结束";return;}
            _busy=true;
            try
            {
                if(_runningService==null)throw new InvalidOperationException("先读取Effective");
                string version;RequireEffective(out version);
                if(version!=_effectiveVersion)throw new InvalidOperationException("Effective已变化，须重新装配");
                await _runningService.StartAsync(CancellationToken.None);
                MLCServiceModuleObservation observed=_runningService.Observe();
                _status.Text="Service生命周期="+observed.State+
                    "，本地请求Ready证据="+
                    (observed.VerifiedReady.HasValue?
                        (observed.VerifiedReady.Value?"已核验":"不可用"):"未知")+
                    "；配置="+_effectiveVersion;
            }
            catch(Exception ex){_status.Text="Service启动拒绝："+ex.Message;}
            finally{_busy=false;}
        }
        public async Task RequestAsync()
        {
            if(_busy){_status.Text="服务操作尚未结束";return;}
            _busy=true;
            try
            {
                if(_runningService==null)throw new InvalidOperationException("先读取Effective并启动");
                string version;RequireEffective(out version);
                if(version!=_effectiveVersion)throw new InvalidOperationException("Effective已变化");
                var reply=await _runningService.RequestAsync(_requestId.Text,_payload.Text,
                    CancellationToken.None);
                LastResponse=reply;
                _observations.Add(reply);
                _responses.ItemsSource=_observations.ToArray();
                _status.Text="真实本地Service响应：RequestId="+reply.RequestId+
                    "，Sequence="+reply.Sequence+", SHA256="+reply.Sha256+
                    "，Output="+reply.Output+"，UniqueRequests="+_runningService.UniqueRequests;
            }
            catch(Exception ex){_status.Text="Service请求拒绝："+ex.Message;}
            finally{_busy=false;}
        }
        public async Task StopAsync()
        {
            if(_busy){_status.Text="服务操作尚未结束";return;}
            _busy=true;
            try
            {
                if(_runningService==null)throw new InvalidOperationException("尚未装配Service");
                await _runningService.StopAsync(CancellationToken.None);
                _status.Text="Service真实生命周期Stopped；新请求必须拒绝。";
            }
            catch(Exception ex){_status.Text="Service停止失败："+ex.Message;}
            finally{_busy=false;}
        }
        public void Close()
        {
            if(_busy)throw new InvalidOperationException("Service请求运行中不能释放资源");
            if(_runningService!=null&&_runningService.IsReady)
                _runningService.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
