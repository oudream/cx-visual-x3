using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Configuration;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Read-only host evidence. An OS-discovered COM resource or DLL file is NOT
    /// a usable vendor Driver, SDK session, physical readback or safety authorization.
    /// </summary>
    public sealed class TCTDriverProbeResult
    {
        public string ModuleId {get;internal set;}
        public string DriverTypeId {get;internal set;}
        public string DeviceRef {get;internal set;}
        public string EffectiveVersion {get;internal set;}
        public string Backend {get;internal set;}
        public bool ConfigAccepted {get;internal set;}
        public bool SdkFileObserved {get;internal set;}
        public string SdkFileVersion {get;internal set;}
        public bool OsPortObserved {get;internal set;}
        public bool ProviderResolved {get{return false;}}
        public bool DeviceReady {get{return false;}}
        public bool PhysicalActionVerified {get{return false;}}
    }

    /// <summary>
    /// Driver TestCenter G1 diagnostic gateway. No SDK is loaded and no hardware
    /// command is issued; only a properly bound vendor Provider and HIL can verify
    /// actual device actions. Preserves MHS/FCM as the sole configuration owner.
    /// </summary>
    public sealed class TCTDriverReadinessPanel : UserControl
    {
        private readonly MHSConfigService _service;
        private readonly Func<bool> _hasUnsavedDraft;
        private readonly TextBox _sdkFile=new TextBox {Width=360,Margin=new Thickness(4),
            ToolTip="仅查询本机文件版本，不加载DLL，不代表Vendor SDK已装配或可执行"};
        private readonly Button _probe=new Button {Content="读取真实设备环境（只读）",
            Margin=new Thickness(4),Padding=new Thickness(10,5,10,5)};
        private readonly TextBlock _summary=new TextBlock {
            Margin=new Thickness(4),TextWrapping=TextWrapping.Wrap
        };
        private readonly DataGrid _evidence=new DataGrid {
            IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,
            CanUserDeleteRows=false,MaxHeight=205,Margin=new Thickness(4)
        };
        private string _moduleId;
        public TCTDriverProbeResult LastProbe {get;private set;}
        public string StatusMessage {get{return _summary.Text;}}
        public TCTDriverReadinessPanel(MHSConfigService service,Func<bool> hasUnsavedDraft)
        {
            _service=service??throw new ArgumentNullException("service");
            _hasUnsavedDraft=hasUnsavedDraft??throw new ArgumentNullException("hasUnsavedDraft");
            AutomationProperties.SetAutomationId(this,"TCT.Driver.ProbePanel");
            AutomationProperties.SetAutomationId(_sdkFile,"TCT.Driver.SdkFile");
            AutomationProperties.SetAutomationId(_probe,"TCT.Driver.Probe");
            AutomationProperties.SetAutomationId(_summary,"TCT.Driver.ProbeStatus");
            AutomationProperties.SetAutomationId(_evidence,"TCT.Driver.ProbeEvidence");
            var pane=new StackPanel {Margin=new Thickness(6)};
            pane.Children.Add(new TextBlock {Text="Driver · 设备/SDK预检（非实机执行）",
                FontWeight=FontWeights.SemiBold,FontSize=15});
            pane.Children.Add(new TextBlock {Text=
                "要求先完成正式Driver配置与任务边界生效。这里只读取已存在的MHS Effective、"+
                "查询Windows端口清单及可选SDK DLL文件版本；发现资源不代表设备已连接、已授权或执行完成。",
                Margin=new Thickness(4,5,4,6),TextWrapping=TextWrapping.Wrap});
            var sdk=new WrapPanel();
            sdk.Children.Add(new TextBlock {Text="厂商SDK DLL（可选，只读）",
                VerticalAlignment=VerticalAlignment.Center});
            sdk.Children.Add(_sdkFile);
            pane.Children.Add(sdk);pane.Children.Add(_probe);pane.Children.Add(_summary);
            pane.Children.Add(_evidence);
            pane.Children.Add(new TextBlock {Text=
                "下一门槛：接入真实专业Provider、设备身份/固件回读、资源独占、控制/采集结果及"+
                "实际安全联锁；没有SDK/设备/HIL证据时一律不能判定DeviceReady或PhysicalCompleted。",
                Margin=new Thickness(4,9,4,0),TextWrapping=TextWrapping.Wrap});
            Content=new ScrollViewer {Content=pane,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=460};
            _probe.Click+=(s,e)=>Probe();
            Bind(null);
        }
        public void Bind(string id)
        {
            if(_moduleId==id)return;
            _moduleId=id;LastProbe=null;_evidence.ItemsSource=null;
            _summary.Text=String.IsNullOrEmpty(id)?"请先打开Driver正式配置。":
                "模块 "+id+"：尚未查询实机环境；配置保存不等于设备就绪。";
        }
        public void SetSdkFile(string path){_sdkFile.Text=path??"";}
        public void Probe()
        {
            LastProbe=null;_evidence.ItemsSource=null;
            try
            {
                if(String.IsNullOrWhiteSpace(_moduleId))
                    throw new InvalidOperationException("请先打开Driver正式配置");
                if(_hasUnsavedDraft())
                    throw new InvalidOperationException("存在未保存配置草稿，请先保存并生效");
                var identity=new FCMConfigIdentity("CxVisual.Module.Driver","module",_moduleId);
                var section=_service.OpenExisting<MFMDriverModuleConfiguration>(identity);
                MFMDriverModuleConfiguration config;
                using(var snapshot=section.CaptureForTask("testcenter-driver-probe"))
                    config=snapshot.Get();
                config.Validate();
                if(!config.Enabled)
                    throw new InvalidOperationException("Driver模块已禁用，不能认定可用");
                if(String.IsNullOrWhiteSpace(config.DeviceRef))
                    throw new InvalidOperationException("Driver未声明MEM设备资源引用DeviceRef");
                string path=_sdkFile.Text.Trim();
                bool fileExists=path.Length!=0&&File.Exists(path);
                string version="";
                if(fileExists)
                {
                    var info=FileVersionInfo.GetVersionInfo(path);
                    version=info.FileVersion??"无文件版本信息";
                }
                string[] ports=SerialPort.GetPortNames();
                bool portFound=ports.Any(x=>String.Equals(x,config.DeviceRef,
                    StringComparison.OrdinalIgnoreCase));
                var result=new TCTDriverProbeResult {
                    ModuleId=_moduleId,DriverTypeId=config.DriverTypeId,
                    DeviceRef=config.DeviceRef,Backend=config.Backend.ToString(),
                    EffectiveVersion=section.EffectiveVersion,ConfigAccepted=true,
                    SdkFileObserved=fileExists,SdkFileVersion=version,OsPortObserved=portFound
                };
                LastProbe=result;
                _evidence.ItemsSource=new [] {
                    new {Check="FCM/MHS Effective",Evidence=section.EffectiveVersion,
                        Conclusion="配置已生效；不是设备动作"},
                    new {Check="DriverTypeId",Evidence=config.DriverTypeId,
                        Conclusion="已声明；未解析真实专业Provider"},
                    new {Check="DeviceRef",Evidence=config.DeviceRef,
                        Conclusion=portFound?"Windows枚举到同名COM端口；未打开或回读":
                            "未匹配Windows COM端口；非串口SDK设备需厂商发现"},
                    new {Check="厂商SDK文件",Evidence=path.Length==0?"未指定":path,
                        Conclusion=fileExists?"已找到文件版本 "+version+"；未执行SDK初始化":
                            "未验证SDK依赖（未指定或不存在）"},
                    new {Check="物理完成和安全联锁",Evidence="无实机HIL证据",
                        Conclusion="拒绝标记Ready/PhysicalCompleted"}
                };
                _summary.Text="已完成真实环境只读检查；Module="+_moduleId+
                    " / Effective="+result.EffectiveVersion+
                    " / SDK文件="+(fileExists?"存在":"未核实")+
                    " / 同名COM端口="+(portFound?"已枚举":"未枚举")+
                    "；DeviceReady=否，PhysicalCompleted=未验证。";
            }
            catch(Exception ex)
            {
                _summary.Text="Driver预检拒绝："+ex.Message+
                    "；没有执行设备操作，也没有伪造成功。";
            }
        }
    }
}
