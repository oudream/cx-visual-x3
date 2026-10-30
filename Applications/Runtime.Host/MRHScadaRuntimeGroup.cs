using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Runtime.Host
{
    /// <summary>MEM持久DeviceRef与MHS Connector/Scope的一次不可变部署装配指令。
    /// 不存规约地址、采样值或另一个工程树；点表仍来自MHS Effective。</summary>
    internal sealed class MRHScadaDeviceLaunch
    {
        internal MRHScadaDeviceLaunch(Guid deviceNodeId,string connectorModuleId,
            string scopeId,int intervalMs,bool contiguous)
        {
            if(deviceNodeId==Guid.Empty||string.IsNullOrWhiteSpace(connectorModuleId)||
                string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("SCADA组装配身份或Scope不合法");
            if(intervalMs<20||intervalMs>3600000)
                throw new ArgumentOutOfRangeException("intervalMs");
            DeviceNodeId=deviceNodeId;ConnectorModuleId=connectorModuleId.Trim();
            ScopeId=scopeId.Trim();IntervalMs=intervalMs;Contiguous=contiguous;
        }
        internal Guid DeviceNodeId {get;private set;}
        internal string ConnectorModuleId {get;private set;}
        internal string ScopeId {get;private set;}
        internal int IntervalMs {get;private set;}
        internal bool Contiguous {get;private set;}
    }

    /// <summary>
    /// 一个进程装配多套已经存在的SCADA设备Session。唯一数据Owner仍是每个MDS Registry，
    /// 连接所有权仍在各自MCN/F17；本类仅负责身份冲突门禁及启动/停止补偿。
    /// 不管理报警/趋势、第二套调度器或跨设备事务。
    /// </summary>
    internal sealed class MRHScadaRuntimeGroup : IDisposable
    {
        private readonly List<MRHScadaRuntime> _devices;
        private bool _started, _stopped, _disposed;

        private MRHScadaRuntimeGroup(List<MRHScadaRuntime> devices)
        { _devices=devices; }

        internal static MRHScadaRuntimeGroup Open(string projectPath,string configRoot,
            IEnumerable<MRHScadaDeviceLaunch> definitions)
        {
            if(definitions==null)throw new ArgumentNullException("definitions");
            MRHScadaDeviceLaunch[] configs=definitions.ToArray();
            if(configs.Length<1||configs.Length>32||configs.Any(x=>x==null))
                throw new ArgumentException("运行Host必须有1～32个明确设备装配");
            if(configs.Select(x=>x.DeviceNodeId).Distinct().Count()!=configs.Length ||
                configs.Select(x=>x.ScopeId).Distinct(StringComparer.Ordinal).Count()!=configs.Length ||
                configs.Select(x=>x.ConnectorModuleId).Distinct(StringComparer.Ordinal).Count()!=configs.Length)
                throw new InvalidOperationException(
                    "SCADA双设备不能复用DeviceRef、MDS Scope或Connector配置身份");
            List<MRHScadaRuntime> prepared=new List<MRHScadaRuntime>();
            try
            {
                HashSet<string> channels=new HashSet<string>(StringComparer.Ordinal);
                foreach(MRHScadaDeviceLaunch config in configs)
                {
                    MRHScadaRuntime runtime=MRHScadaRuntime.Open(projectPath,configRoot,
                        config.DeviceNodeId,config.ConnectorModuleId,
                        config.ScopeId,config.IntervalMs,config.Contiguous);
                    prepared.Add(runtime);
                    if(!channels.Add(runtime.Device.ChannelId))
                        throw new InvalidOperationException(
                            "SCADA两个设备复用了同一F17 ChannelId，拒绝装配");
                }
                return new MRHScadaRuntimeGroup(prepared);
            }
            catch
            {
                for(int i=prepared.Count-1;i>=0;i--)
                    try{prepared[i].Dispose();}catch{}
                throw;
            }
        }

        internal int Count {get{return _devices.Count;}}
        internal MRHScadaRuntime Find(Guid deviceNodeId)
        {
            string key=deviceNodeId.ToString("N");
            MRHScadaRuntime device=_devices.FirstOrDefault(x=>x.DeviceRef==key);
            if(device==null)throw new KeyNotFoundException("SCADA工程设备不存在: "+key);
            return device;
        }
        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            if(_started||_stopped||_disposed)
                throw new InvalidOperationException("SCADA设备组不可重复启动");
            _started=true;
            try
            {
                foreach(MRHScadaRuntime device in _devices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await device.StartAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                try{await StopAsync().ConfigureAwait(false);}catch{}
                throw;
            }
        }

        internal async Task StopAsync()
        {
            if(_stopped)return;
            _stopped=true;
            List<Exception> failures=new List<Exception>();
            // 先对所有在途采集发停止，再释放，单个Device故障不阻断其余设备关闭。
            for(int i=_devices.Count-1;i>=0;i--)
                try{await _devices[i].StopAsync().ConfigureAwait(false);}
                catch(Exception error){failures.Add(error);}
            if(failures.Count>0)
                throw new AggregateException("SCADA设备组停止时存在故障",failures);
        }
        public void Dispose()
        {
            if(_disposed)return;
            _disposed=true;
            try{StopAsync().GetAwaiter().GetResult();}catch{}
            for(int i=_devices.Count-1;i>=0;i--)
                _devices[i].Dispose();
        }
    }
}
