using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Connectivity
{
    public enum MPADataTargetKind { Variable, DataHeap, DataStack, Message, Command, ProfessionalData }
    public enum MPADataDirection { Inbound, Outbound, Both }
    public enum MPADataOperation { ReadValue, WriteValue, PublishMessage, InvokeCommand }
    public enum MPADataTransformKind { None, Boolean, Int16, UInt16, Int32, UInt32, Int64, Single, Double, Text }

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
        [FCMConfigItem("值转换", Order = 6)]
        public MPADataTransformKind Transform { get; set; } = MPADataTransformKind.None;
        [FCMConfigItem("失败是否终止", Order = 7)]
        public bool Required { get; set; } = true;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PointRef) || string.IsNullOrWhiteSpace(TargetRef) ||
                !Enum.IsDefined(typeof(MPADataTargetKind), TargetKind) ||
                !Enum.IsDefined(typeof(MPADataDirection), Direction) ||
                !Enum.IsDefined(typeof(MPADataOperation), Operation) ||
                !Enum.IsDefined(typeof(MPADataTransformKind), Transform))
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

    // 直接管理同一 Protocol.Configuration.Mappings 列表，不创建第二套持久化定义。
    public sealed class MPADataMappingCollection
    {
        private readonly MPAProtocolConfiguration _owner;
        public MPADataMappingCollection(MPAProtocolConfiguration owner)
        { _owner = owner ?? throw new ArgumentNullException("owner"); }
        public void Add(MPADataMapping mapping)
        {
            if (mapping == null) throw new ArgumentNullException("mapping");
            mapping.Validate();
            if (!_owner.Points.Any(x => x.Name == mapping.PointRef))
                throw new ArgumentException("映射点不存在：" + mapping.PointRef);
            _owner.Mappings.Add(mapping);
            try { _owner.Validate(); }
            catch { _owner.Mappings.RemoveAt(_owner.Mappings.Count - 1); throw; }
        }
        public bool Remove(MPADataMapping mapping) { return _owner.Mappings.Remove(mapping); }
        public IReadOnlyList<MPADataMapping> Find(string pointName)
        { return _owner.Mappings.Where(x => x.PointRef == pointName).ToArray(); }
        public void Validate() { _owner.Validate(); }
    }

    public static class MPADataValueTransform
    {
        public static object Apply(object input, MPADataTransformKind transform)
        {
            if (transform == MPADataTransformKind.None || input == null) return input;
            Type output;
            switch (transform)
            {
                case MPADataTransformKind.Boolean: output = typeof(bool); break;
                case MPADataTransformKind.Int16: output = typeof(short); break;
                case MPADataTransformKind.UInt16: output = typeof(ushort); break;
                case MPADataTransformKind.Int32: output = typeof(int); break;
                case MPADataTransformKind.UInt32: output = typeof(uint); break;
                case MPADataTransformKind.Int64: output = typeof(long); break;
                case MPADataTransformKind.Single: output = typeof(float); break;
                case MPADataTransformKind.Double: output = typeof(double); break;
                case MPADataTransformKind.Text: output = typeof(string); break;
                default: throw new ArgumentOutOfRangeException("transform");
            }
            if (input is byte[]) throw new InvalidCastException("原始缓冲需由专业规约显式解码，不能隐式转换");
            return Convert.ChangeType(input, output, CultureInfo.InvariantCulture);
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
                            await _owner.WriteAsync(item.TargetKind, item.TargetRef, MPADataValueTransform.Apply(value, item.Transform), ct).ConfigureAwait(false);
                            break;
                        case MPADataOperation.PublishMessage:
                            await _owner.PublishAsync(item.TargetRef, MPADataValueTransform.Apply(value, item.Transform), ct).ConfigureAwait(false);
                            break;
                        case MPADataOperation.InvokeCommand:
                            await _owner.InvokeAsync(item.TargetRef, MPADataValueTransform.Apply(value, item.Transform), ct).ConfigureAwait(false);
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
            return MPADataValueTransform.Apply(await _owner.ReadAsync(source.TargetKind,
                source.TargetRef, ct).ConfigureAwait(false), source.Transform);
        }
    }
}
