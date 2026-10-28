using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Professional.Vision;
using CxVisual.Professional.Vision.Modules;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Real Compute CPU test workspace inside the existing five-kind Module Center.
    /// MHS owns both FCM module config and Vision parameters; MEM/MOC/MRK and
    /// the real algorithm are executed only by Professional.Vision's shared runner.
    /// This is a local software test, not a hardware/production approval.
    /// </summary>
    public sealed class TCTCpuComputePanel : UserControl
    {
        private readonly MHSConfigService _service;
        private readonly Func<bool> _hasUnsavedDraft;
        private readonly TextBox _path=Text("TCT.Cpu.RawPath","",300);
        private readonly TextBox _width=Text("TCT.Cpu.Width","5",60);
        private readonly TextBox _height=Text("TCT.Cpu.Height","3",60);
        private readonly TextBox _threshold=Text("TCT.Cpu.Threshold","200",65);
        private readonly TextBox _minArea=Text("TCT.Cpu.MinArea","2",65);
        private readonly TextBox _maxArea=Text("TCT.Cpu.MaxArea","100",70);
        private readonly ComboBox _depth=new ComboBox {Width=85,Margin=new Thickness(4)};
        private readonly TextBlock _status=new TextBlock {TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(4,5,4,4)};
        private readonly DataGrid _regions=new DataGrid {IsReadOnly=true,AutoGenerateColumns=true,
            MaxHeight=145,Visibility=Visibility.Collapsed,CanUserAddRows=false};
        private readonly DataGrid _trace=new DataGrid {IsReadOnly=true,AutoGenerateColumns=true,
            MaxHeight=145,Visibility=Visibility.Collapsed,CanUserAddRows=false};
        private readonly Button _save=Button("TCT.Cpu.Save","保存/生效Vision参数");
        private readonly Button _load=Button("TCT.Cpu.Load","读取当前Effective");
        private readonly Button _run=Button("TCT.Cpu.Run","执行真实RAW检测");
        private readonly Button _cancel=Button("TCT.Cpu.Cancel","取消");
        private string _moduleId;
        private CxVisionThresholdProvider _provider;
        private MEMEngineeringSession _session;
        private CancellationTokenSource _running;
        public CxVisionCpuRun LastReport {get;private set;}
        public string StatusMessage {get{return _status.Text;}}
        public bool IsRunning {get{return _running!=null;}}
        public TCTCpuComputePanel(MHSConfigService service,Func<bool> hasUnsavedDraft)
        {
            _service=service??throw new ArgumentNullException("service");
            _hasUnsavedDraft=hasUnsavedDraft??throw new ArgumentNullException("hasUnsavedDraft");
            _depth.ItemsSource=Enum.GetValues(typeof(CxVisionPixelDepth));
            _depth.SelectedItem=CxVisionPixelDepth.Bits8;
            AutomationProperties.SetAutomationId(this,"TCT.Cpu.Panel");
            AutomationProperties.SetAutomationId(_depth,"TCT.Cpu.PixelDepth");
            AutomationProperties.SetAutomationId(_status,"TCT.Cpu.Status");
            AutomationProperties.SetAutomationId(_regions,"TCT.Cpu.Regions");
            AutomationProperties.SetAutomationId(_trace,"TCT.Cpu.Trace");
            _cancel.IsEnabled=false;
            var outer=new StackPanel {Margin=new Thickness(4)};
            outer.Children.Add(new TextBlock {Text="Compute · CPU RAW检测（真实软件；不触发设备）",
                FontWeight=FontWeights.SemiBold});
            outer.Children.Add(new TextBlock {Text=
                "先在上方正式Compute配置中设置ProviderTypeId=vision.cpu.threshold.inspect、Device=Cpu，"+
                "声明相同CapabilityId并保存/生效；此处Vision参数也是MHS正式配置。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,2,2,4)});
            var inputs=new WrapPanel();
            Add(inputs,"RAW文件",_path);
            var browse=Button("TCT.Cpu.Browse","选择文件");
            browse.Click+=(s,e)=>{
                var dialog=new Microsoft.Win32.OpenFileDialog {
                    Title="选择真实RAW灰度数据",Filter="RAW (*.raw)|*.raw|全部文件|*.*",
                    CheckFileExists=true};
                if(dialog.ShowDialog()==true)_path.Text=dialog.FileName;
            };
            inputs.Children.Add(browse);
            Add(inputs,"宽",_width);Add(inputs,"高",_height);
            outer.Children.Add(inputs);
            var settings=new WrapPanel();
            Add(settings,"位深",_depth);Add(settings,"阈值",_threshold);
            Add(settings,"最小面积",_minArea);Add(settings,"最大面积",_maxArea);
            settings.Children.Add(_save);settings.Children.Add(_load);
            outer.Children.Add(settings);
            var operations=new WrapPanel();
            operations.Children.Add(_run);operations.Children.Add(_cancel);
            outer.Children.Add(operations);outer.Children.Add(_status);
            outer.Children.Add(new TextBlock {Text="缺陷区域（真实CPU结果）",
                FontWeight=FontWeights.SemiBold});
            outer.Children.Add(_regions);
            outer.Children.Add(new TextBlock {Text="MRK / MOC SourceMap真实Trace",
                FontWeight=FontWeights.SemiBold});
            outer.Children.Add(_trace);
            Content=new ScrollViewer {Content=outer,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                MaxHeight=470};
            _save.Click+=async(s,e)=>await SaveEffectiveAsync();
            _load.Click+=(s,e)=>LoadEffective();
            _run.Click+=async(s,e)=>await RunAsync();
            _cancel.Click+=(s,e)=>{if(_running!=null)_running.Cancel();};
            _status.Text="请选择Compute模块并打开正式模块配置。";
        }
        private static TextBox Text(string id,string value,double width)
        {
            var box=new TextBox {Text=value,Width=width,Margin=new Thickness(4),
                Padding=new Thickness(4,2,4,2)};
            AutomationProperties.SetAutomationId(box,id);return box;
        }
        private static Button Button(string id,string title)
        {
            var b=new Button {Content=title,Margin=new Thickness(4),Padding=new Thickness(8,4,8,4)};
            AutomationProperties.SetAutomationId(b,id);return b;
        }
        private static void Add(Panel panel,string title,Control control)
        {
            panel.Children.Add(new TextBlock {Text=title,VerticalAlignment=VerticalAlignment.Center});
            panel.Children.Add(control);
        }
        public void Bind(string id)
        {
            if(_running!=null)throw new InvalidOperationException("正在检测，禁止切换模块");
            if(_moduleId==id)return;
            _moduleId=id;_provider=null;_session=null;LastReport=null;
            _regions.ItemsSource=null;_trace.ItemsSource=null;
            _regions.Visibility=Visibility.Collapsed;_trace.Visibility=Visibility.Collapsed;
            _status.Text=String.IsNullOrEmpty(id)?"请先打开正式Compute配置":
                "已关联模块 "+id+"；请保存并生效CPU专业参数，然后显式加载Effective。";
        }
        public void SetRawInput(string path,int width,int height)
        {
            _path.Text=path??"";_width.Text=width.ToString(CultureInfo.InvariantCulture);
            _height.Text=height.ToString(CultureInfo.InvariantCulture);
        }
        public void SetVisionParameters(CxVisionPixelDepth depth,int threshold,double min,double max)
        {
            _depth.SelectedItem=depth;
            _threshold.Text=threshold.ToString(CultureInfo.InvariantCulture);
            _minArea.Text=min.ToString(CultureInfo.InvariantCulture);
            _maxArea.Text=max.ToString(CultureInfo.InvariantCulture);
        }
        private string RequireScope()
        {
            if(String.IsNullOrWhiteSpace(_moduleId))
                throw new InvalidOperationException("先打开正式Compute模块配置");
            if(_hasUnsavedDraft())throw new InvalidOperationException("请先保存正式Compute模块配置草稿");
            return _moduleId;
        }
        private void RequireFormalCompute(string id)
        {
            var section=_service.ForScope("module",id)
                .OpenExisting<PVCComputeModuleConfiguration>();
            PVCComputeModuleConfiguration config;
            using(var capture=section.CaptureForTask("cpu-panel-config"))
                config=capture.Get();
            if(config.Device!=PVCComputeDevicePreference.Cpu ||
                config.ProviderTypeId!=CxVisionThresholdProvider.CapabilityId ||
                config.CapabilityIds==null ||
                !config.CapabilityIds.Contains(CxVisionThresholdProvider.CapabilityId))
                throw new InvalidOperationException(
                    "正式Compute Effective配置须指定CPU、真实ProviderTypeId及CapabilityIds后再执行");
        }
        public async Task SaveEffectiveAsync()
        {
            if(_running!=null){_status.Text="运行中不能修改配置";return;}
            try
            {
                string id=RequireScope();
                int gray=int.Parse(_threshold.Text,CultureInfo.InvariantCulture);
                double min=double.Parse(_minArea.Text,CultureInfo.InvariantCulture);
                double max=double.Parse(_maxArea.Text,CultureInfo.InvariantCulture);
                var depth=(CxVisionPixelDepth)_depth.SelectedItem;
                // Save only on explicit click: never initialize default configs during a read.
                var section=_service.ForScope("module",id).Open<CxVisionInspectionOptions>();
                var committed=await section.UpdateAsync(model=>{
                    model.PixelDepth=depth;model.GrayThreshold=gray;
                    model.MinDefectArea=min;model.MaxDefectArea=max;model.UseAi=false;
                },"TestCenter真实CPU专业参数",CancellationToken.None);
                if(!committed.Success)throw new InvalidOperationException(
                    committed.ErrorCode??"FCM验证/提交失败");
                if(section.HasPendingApply&&!section.ApplyPendingAtTaskBoundary())
                    throw new InvalidOperationException("MHS任务边界未能生效");
                _provider=null;
                _status.Text="Vision已保存并生效："+section.EffectiveVersion+
                    "，请重新显式读取Effective；这不是设备运行。";
            }
            catch(Exception e){_status.Text="参数保存/生效失败："+e.Message;}
        }
        public void LoadEffective()
        {
            if(_running!=null){_status.Text="检测运行中不可切换配置";return;}
            _provider=null;
            try
            {
                string id=RequireScope();
                RequireFormalCompute(id);
                var vision=_service.ForScope("module",id)
                    .OpenExisting<CxVisionInspectionOptions>();
                _provider=CxVisionThresholdProvider.FromEffectiveConfiguration(vision);
                string version;
                if(!_provider.TryGetCapabilityVersion(
                    CxVisionThresholdProvider.CapabilityId,out version))
                    throw new InvalidOperationException("CPU Provider未解析有效版本");
                _status.Text="已读取正式Compute/Vision Effective "+version+
                    "；仅本地CPU软件操作，不代表硬件授权。";
            }
            catch(Exception e){_status.Text="Effective读取拒绝："+e.Message;}
        }
        public async Task RunAsync()
        {
            if(_running!=null){_status.Text="检测仍在运行";return;}
            LastReport=null;_regions.ItemsSource=null;_trace.ItemsSource=null;
            _regions.Visibility=Visibility.Collapsed;_trace.Visibility=Visibility.Collapsed;
            try
            {
                string id=RequireScope();
                if(_provider==null)throw new InvalidOperationException("先显式读取有效配置");
                RequireFormalCompute(id);
                string version;
                if(!_provider.TryGetCapabilityVersion(
                    CxVisionThresholdProvider.CapabilityId,out version))
                    throw new InvalidOperationException("Effective版本已变更，请重新读取");
                int w=int.Parse(_width.Text,CultureInfo.InvariantCulture);
                int h=int.Parse(_height.Text,CultureInfo.InvariantCulture);
                string filename=_path.Text.Trim();
                // Invalid path/size must fail before an engineering page is created.
                CxVisionRawCpuRunner.ReadRaw(filename,w,h,_provider.PixelDepth,
                    CancellationToken.None);
                if(_session==null)_session=MEMEngineering.Create("TCT Compute "+id);
                _running=new CancellationTokenSource();
                _run.IsEnabled=false;_save.IsEnabled=false;_load.IsEnabled=false;
                _cancel.IsEnabled=true;_status.Text="实际CPU算法/原MOC-MRK执行中…";
                var report=await Task.Run(()=>CxVisionRawCpuRunner.ExecuteAsync(
                    _session,_provider,filename,w,h,_running.Token),_running.Token);
                LastReport=report;
                _regions.ItemsSource=report.Inspection.Regions.Select((x,i)=>new {
                    No=i+1,Area=x.PixelArea,MinX=x.MinX,MinY=x.MinY,MaxX=x.MaxX,MaxY=x.MaxY
                }).ToArray();
                _regions.Visibility=Visibility.Visible;
                _trace.ItemsSource=report.Trace.Select(x=>new {
                    x.Sequence,x.StepId,x.BlockId,x.State,x.TimestampUtc
                }).ToArray();
                _trace.Visibility=Visibility.Visible;
                _status.Text="实际检测"+(report.Inspection.Passed?"通过":"发现缺陷")+
                    "；阈值像素="+report.Inspection.ThresholdPixelCount+
                    "；缺陷区域="+report.Inspection.Regions.Count+
                    "；FlowId="+report.FlowId.ToString("N")+
                    "；MOC指纹="+report.Fingerprint+
                    "；配置="+report.ConfigVersion+
                    "；真实Trace="+report.CompletedTraceEvents+
                    "；原始RAW未作为持久资源保存。";
            }
            catch(OperationCanceledException){_status.Text="CPU检测已取消，不宣称完成";}
            catch(Exception e){_status.Text="CPU检测失败："+e.Message;}
            finally
            {
                if(_running!=null){_running.Dispose();_running=null;}
                _cancel.IsEnabled=false;_run.IsEnabled=true;_save.IsEnabled=true;_load.IsEnabled=true;
            }
        }
        public void Stop(){if(_running!=null)_running.Cancel();}
    }
}
