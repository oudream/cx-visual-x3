using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    public enum MPAProtocolSessionState { Created, Active, Stopped, Faulted }

    public interface IMPAProtocolSession : IDisposable
    {
        string SessionId { get; }
        string BindingId { get; }
        MPAProtocolSessionState State { get; }
        void Start();
        void Stop();
    }

    // 仅负责规约运行会话的身份和状态。I/O、事务执行在具体 Protocol 内直接实现；
    // 不重复创建 Runtime Scheduler，也不在此实现第二套 Socket 或 Channel Adapter。
    public sealed class MPAProtocolSession : IMPAProtocolSession
    {
        private readonly object _gate = new object();
        private MPAProtocolSessionState _state;
        private bool _disposed;
        public MPAProtocolSession(MPAProtocolConfiguration binding, MCCSubChannelAddress channelAddress = null)
        {
            if (binding == null) throw new ArgumentNullException("binding");
            binding.Validate();
            // Connector 唯一绑定 Channel，Session 仅保留其运行地址；配置不保存第二份 ChannelRef。
            BindingId = binding.Id; ChannelAddress = channelAddress;
            SessionId = Guid.NewGuid().ToString("N"); _state = MPAProtocolSessionState.Created;
        }
        public string SessionId { get; private set; }
        public string BindingId { get; private set; }
        public MCCSubChannelAddress ChannelAddress { get; private set; }
        public MPAProtocolSessionState State { get { lock (_gate) return _state; } }
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MPAProtocolSession");
                if (_state != MPAProtocolSessionState.Created && _state != MPAProtocolSessionState.Stopped)
                    throw new InvalidOperationException("规约会话无法从当前状态启动");
                _state = MPAProtocolSessionState.Active;
            }
        }
        public void Stop()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MPAProtocolSession");
                if (_state == MPAProtocolSessionState.Active) _state = MPAProtocolSessionState.Stopped;
            }
        }
        public void Dispose()
        {
            lock (_gate) { _state = MPAProtocolSessionState.Stopped; _disposed = true; }
        }
    }

    public sealed class MPAProtocolContext
    {
        public MPAProtocolContext(string bindingId, string sessionId,
            MCCSubChannelAddress channelAddress = null, string correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(bindingId)) throw new ArgumentException("BindingId 必填");
            BindingId = bindingId.Trim(); SessionId = sessionId;
            ChannelAddress = channelAddress; CorrelationId = correlationId;
        }
        public string BindingId { get; private set; }
        public string SessionId { get; private set; }
        public MCCSubChannelAddress ChannelAddress { get; private set; }
        public string CorrelationId { get; private set; }
        // MDS、未来消息/命令 Owner 在调用边界解析，不由此持有数据仓库。
    }
}
