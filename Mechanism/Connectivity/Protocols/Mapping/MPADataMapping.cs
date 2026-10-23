using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Connectivity
{
    public enum MPADataTargetKind { Variable, DataHeap, DataStack, Message, Command, ProfessionalData }
    public enum MPADataDirection { Inbound, Outbound, Both }
    public enum MPADataOperation { ReadValue, WriteValue, PublishMessage, InvokeCommand }

    public sealed class MPADataMapping
    {
        [FCMConfigItem("来源点 Id", Order = 1)] [Required]
        public string PointRef { get; set; } = "";
        [FCMConfigItem("数据目标类别", Order = 2)]
        public MPADataTargetKind TargetKind { get; set; } = MPADataTargetKind.Variable;
        [FCMConfigItem("数据目标引用", Order = 3)] [Required]
        public string TargetRef { get; set; } = "";
        [FCMConfigItem("映射方向", Order = 4)]
        public MPADataDirection Direction { get; set; } = MPADataDirection.Inbound;
        [FCMConfigItem("目标操作", Order = 5)]
        public MPADataOperation Operation { get; set; } = MPADataOperation.WriteValue;
        [FCMConfigItem("失败是否终止", Order = 6)]
        public bool Required { get; set; } = true;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PointRef) || string.IsNullOrWhiteSpace(TargetRef) ||
                !Enum.IsDefined(typeof(MPADataTargetKind), TargetKind) ||
                !Enum.IsDefined(typeof(MPADataDirection), Direction) ||
                !Enum.IsDefined(typeof(MPADataOperation), Operation))
                throw new ArgumentException("数据映射引用/类别/方向/操作无效");
            if ((Operation == MPADataOperation.PublishMessage && TargetKind != MPADataTargetKind.Message) ||
                (Operation == MPADataOperation.InvokeCommand && TargetKind != MPADataTargetKind.Command) ||
                ((Operation == MPADataOperation.ReadValue || Operation == MPADataOperation.WriteValue) &&
                 (TargetKind == MPADataTargetKind.Message || TargetKind == MPADataTargetKind.Command)))
                throw new ArgumentException("映射操作与 Owner 类型不匹配");
            if ((Direction == MPADataDirection.Inbound && Operation == MPADataOperation.ReadValue) ||
                (Direction == MPADataDirection.Outbound && Operation != MPADataOperation.ReadValue))
                throw new ArgumentException("映射方向与操作不兼容");
        }
    }

    // Owner 数据行为由 MDS / 后续 Message / Command 真实实现，不另建运行时存储或消息总线。
    public interface IMPADataTargetAccess
    {
        Task<object> ReadAsync(MPADataTargetKind kind, string targetRef, CancellationToken ct);
        Task WriteAsync(MPADataTargetKind kind, string targetRef, object value, CancellationToken ct);
        Task PublishAsync(string messageRef, object value, CancellationToken ct);
        Task InvokeAsync(string commandRef, object value, CancellationToken ct);
    }

    public sealed class MPADataMappingExecutor
    {
        private readonly IMPADataTargetAccess _owner;
        public MPADataMappingExecutor(IMPADataTargetAccess owner)
        { _owner = owner ?? throw new ArgumentNullException("owner"); }
        public async Task ApplyInboundAsync(MPAProtocolConfiguration configuration,
            string pointName, object value, CancellationToken ct)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            ct.ThrowIfCancellationRequested();
            configuration.Validate();
            MPAPointDefinition point = configuration.Points.FirstOrDefault(p => p.Name == pointName);
            if (point == null || !point.CanRead) throw new InvalidOperationException("点不存在或不允许读取");
            foreach (MPADataMapping item in configuration.Mappings.Where(x => x.PointRef == pointName &&
                x.Direction != MPADataDirection.Outbound && x.Operation != MPADataOperation.ReadValue))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    switch (item.Operation)
                    {
                        case MPADataOperation.WriteValue:
                            await _owner.WriteAsync(item.TargetKind, item.TargetRef, value, ct).ConfigureAwait(false);
                            break;
                        case MPADataOperation.PublishMessage:
                            await _owner.PublishAsync(item.TargetRef, value, ct).ConfigureAwait(false);
                            break;
                        case MPADataOperation.InvokeCommand:
                            await _owner.InvokeAsync(item.TargetRef, value, ct).ConfigureAwait(false);
                            break;
                        default:
                            throw new InvalidOperationException("入站操作不支持");
                    }
                }
                catch when (!item.Required && !ct.IsCancellationRequested) { /* 独立非关键映射允许跳过 */ }
            }
        }
        public async Task<object> ReadOutboundAsync(MPAProtocolConfiguration configuration,
            string pointName, CancellationToken ct)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            ct.ThrowIfCancellationRequested();
            configuration.Validate();
            MPAPointDefinition point = configuration.Points.FirstOrDefault(p => p.Name == pointName);
            if (point == null || !point.CanWrite) throw new InvalidOperationException("点不存在或不允许向外写入");
            MPADataMapping[] sources = configuration.Mappings.Where(x => x.PointRef == pointName &&
                x.Direction != MPADataDirection.Inbound && x.Operation == MPADataOperation.ReadValue).ToArray();
            if (sources.Length != 1) throw new InvalidOperationException("出站写入必须有且只有一个明确的数据来源");
            MPADataMapping source = sources[0];
            return await _owner.ReadAsync(source.TargetKind, source.TargetRef, ct).ConfigureAwait(false);
        }
    }
}
