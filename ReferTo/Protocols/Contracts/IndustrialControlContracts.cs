using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Abstractions;

namespace CxVisual.Platform.Protocols
{
    /// <summary>ICS 设备分类。这里只表达工程语义，不包含任何厂商 SDK 类型。</summary>
    public enum IcsDeviceCategory
    {
        Plc,
        Controller,
        RemoteIo,
        Fieldbus,
        Sensor,
        Actuator,
        Safety,
        Process,
        Instrument,
        Other
    }

    public enum IcsConnectionState
    {
        Offline,
        Connecting,
        Online,
        Degraded,
        Faulted
    }

    /// <summary>通信配置资源。Settings 只保存可序列化配置，不保存 Socket/SDK Handle。</summary>
    public sealed class IcsConnectionProfile
    {
        public IcsConnectionProfile()
        {
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ReconnectMinimum = TimeSpan.FromSeconds(1);
            ReconnectMaximum = TimeSpan.FromSeconds(30);
            OperationTimeout = TimeSpan.FromSeconds(5);
        }

        public string ConnectionId { get; set; }
        public string ProviderId { get; set; }
        public string Protocol { get; set; }
        public string Endpoint { get; set; }
        public TimeSpan ReconnectMinimum { get; set; }
        public TimeSpan ReconnectMaximum { get; set; }
        public TimeSpan OperationTimeout { get; set; }
        public Dictionary<string, string> Settings { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ConnectionId)) throw new InvalidOperationException("ICS ConnectionId不能为空。");
            if (string.IsNullOrWhiteSpace(ProviderId)) throw new InvalidOperationException("ICS ProviderId不能为空。");
            if (string.IsNullOrWhiteSpace(Protocol)) throw new InvalidOperationException("ICS Protocol不能为空。");
            if (ReconnectMinimum < TimeSpan.Zero) throw new InvalidOperationException("ICS ReconnectMinimum非法。");
            if (ReconnectMaximum < ReconnectMinimum) throw new InvalidOperationException("ICS ReconnectMaximum不能小于ReconnectMinimum。");
            if (OperationTimeout <= TimeSpan.Zero) throw new InvalidOperationException("ICS OperationTimeout必须大于0。");
        }
    }

    /// <summary>设备内 Tag 定义。运行值、质量、时间戳不写回工程定义。</summary>
    public sealed class IcsTagDefinition
    {
        public string TagId { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string DataType { get; set; }
        public string Unit { get; set; }
        public VariableAccess Access { get; set; }
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public double? Deadband { get; set; }
        public string Description { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(TagId)) throw new InvalidOperationException("ICS TagId不能为空。");
            if (string.IsNullOrWhiteSpace(Name)) Name = TagId;
            if (string.IsNullOrWhiteSpace(Address)) throw new InvalidOperationException("ICS Tag Address不能为空：" + TagId);
            if (Minimum.HasValue && Maximum.HasValue && Minimum.Value > Maximum.Value)
                throw new InvalidOperationException("ICS Tag上下限非法：" + TagId);
            if (Deadband.HasValue && Deadband.Value < 0) throw new InvalidOperationException("ICS Tag Deadband不能小于0：" + TagId);
        }
    }

    /// <summary>ICS 设备工程资源定义。</summary>
    public sealed class IcsDeviceDefinition
    {
        public IcsDeviceDefinition()
        {
            Tags = new List<IcsTagDefinition>();
            Capabilities = new List<string>();
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Enabled = true;
        }

        public string DeviceId { get; set; }
        public string DisplayName { get; set; }
        public IcsDeviceCategory Category { get; set; }
        public string DeviceType { get; set; }
        public string ProviderId { get; set; }
        public string ConnectionId { get; set; }
        public bool Enabled { get; set; }
        public List<IcsTagDefinition> Tags { get; set; }
        public List<string> Capabilities { get; set; }
        public Dictionary<string, string> Settings { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DeviceId)) throw new InvalidOperationException("ICS DeviceId不能为空。");
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = DeviceId;
            if (string.IsNullOrWhiteSpace(DeviceType)) throw new InvalidOperationException("ICS DeviceType不能为空：" + DeviceId);
            if (string.IsNullOrWhiteSpace(ProviderId)) throw new InvalidOperationException("ICS ProviderId不能为空：" + DeviceId);
            if (string.IsNullOrWhiteSpace(ConnectionId)) throw new InvalidOperationException("ICS ConnectionId不能为空：" + DeviceId);

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IcsTagDefinition tag in Tags ?? new List<IcsTagDefinition>())
            {
                if (tag == null) throw new InvalidOperationException("ICS Tag定义不能为空：" + DeviceId);
                tag.Validate();
                if (!ids.Add(tag.TagId)) throw new InvalidOperationException("ICS TagId重复：" + DeviceId + "/" + tag.TagId);
            }
        }

        public IndustrialDeviceProfile ToIndustrialProfile()
        {
            IndustrialDeviceKind kind = IndustrialDeviceKind.Other;
            switch (Category)
            {
                case IcsDeviceCategory.Plc:
                case IcsDeviceCategory.Controller: kind = IndustrialDeviceKind.Controller; break;
                case IcsDeviceCategory.RemoteIo: kind = IndustrialDeviceKind.Io; break;
                case IcsDeviceCategory.Fieldbus: kind = IndustrialDeviceKind.Fieldbus; break;
                case IcsDeviceCategory.Sensor: kind = IndustrialDeviceKind.Sensor; break;
                case IcsDeviceCategory.Actuator: kind = IndustrialDeviceKind.Actuator; break;
                case IcsDeviceCategory.Safety: kind = IndustrialDeviceKind.SafetyDevice; break;
                case IcsDeviceCategory.Process: kind = IndustrialDeviceKind.ProcessDevice; break;
            }

            return new IndustrialDeviceProfile
            {
                DeviceId = DeviceId,
                DisplayName = DisplayName,
                ProviderId = ProviderId,
                DeviceType = DeviceType,
                Kind = kind,
                Enabled = Enabled,
                Settings = new Dictionary<string, string>(Settings ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    /// <summary>原始 PLC/控制器读写只允许维修/诊断链使用，普通 HMI/Logic 不应持有该接口。</summary>
    public interface IIcsMaintenanceAccess
    {
        Task<object> ReadRawAsync(string address, CancellationToken token);
        Task WriteRawAsync(string address, object value, CancellationToken token);
    }

    /// <summary>ICS 运行设备边界：统一设备生命周期 + 工程变量 Provider。</summary>
    public interface IIcsDevice : IIndustrialDevice
    {
        string ConnectionId { get; }
        IVariableProvider VariableProvider { get; }
        IIcsMaintenanceAccess MaintenanceAccess { get; }
        IcsConnectionState ConnectionState { get; }
    }

    /// <summary>驱动插件实现该接口，Automation Core 不接触厂商 SDK。</summary>
    public interface IIcsDeviceProvider : ICapabilityProvider
    {
        string ProviderId { get; }
        string ProviderVersion { get; }
        bool CanCreate(string deviceType);
        IIcsDevice Create(IcsDeviceDefinition definition, IcsConnectionProfile connection);
    }

    public sealed class IcsRecoveryFailure
    {
        public string DeviceId { get; set; }
        public string Error { get; set; }
    }

    /// <summary>一次恢复扫描的结果。单设备失败不能阻断其它设备恢复。</summary>
    public sealed class IcsRecoveryBatchResult
    {
        public IcsRecoveryBatchResult() { Failures = new List<IcsRecoveryFailure>(); }
        public int AttemptedDeviceCount { get; set; }
        public int RecoveredDeviceCount { get; set; }
        public List<IcsRecoveryFailure> Failures { get; set; }
    }

    public sealed class IcsRuntimeSnapshot
    {
        public DateTime TimestampUtc { get; set; }
        public IReadOnlyList<IndustrialDeviceStatus> Devices { get; set; }
        public IReadOnlyDictionary<string, IcsConnectionState> Connections { get; set; }
        public int OnlineDeviceCount { get; set; }
        public int FaultedDeviceCount { get; set; }
        public int VariableCount { get; set; }
    }

    public static class IcsCapabilityIds
    {
        public const string DeviceLifecycle = "ics.device.lifecycle";
        public const string Variables = "ics.variables";
        public const string MaintenanceRawAccess = "ics.maintenance.raw-access";
        public const string Diagnostics = "ics.diagnostics";
    }
}
