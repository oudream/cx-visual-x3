using System;

namespace CxVisual.Foundation.Data.Types
{
    /// <summary>基础数据形态。专业Image/Volume/Tensor具体类型仍归各自基础/专业Owner。</summary>
    public enum FDTDataKind
    {
        Scalar,
        Buffer,
        Object,
        List,
        Map,
        Image,
        Volume,
        Tensor,
        Stream
    }

    /// <summary>无运行状态的数据类型基本描述；不建立第二套端口Schema注册表。</summary>
    public sealed class FDTDataTypeDescriptor
    {
        private FDTDataTypeDescriptor(Type clrType, FDTDataKind kind,
            bool safelyCopyable)
        {
            ClrType = clrType;
            Kind = kind;
            IsSafelyCopyable = safelyCopyable;
            IsNullable = !clrType.IsValueType ||
                Nullable.GetUnderlyingType(clrType) != null;
        }

        public Type ClrType { get; private set; }
        public FDTDataKind Kind { get; private set; }
        public bool IsSafelyCopyable { get; private set; }
        public bool IsNullable { get; private set; }

        public static FDTDataTypeDescriptor Describe(Type type)
        {
            if (type == null || type == typeof(void))
                throw new ArgumentException("无效数据类型", "type");
            if (type == typeof(byte[]))
                return new FDTDataTypeDescriptor(type, FDTDataKind.Buffer, true);
            if (FDTDataValueSafety.IsSimpleImmutable(type))
                return new FDTDataTypeDescriptor(type, FDTDataKind.Scalar, true);
            return new FDTDataTypeDescriptor(type,
                type.IsArray ? FDTDataKind.List : FDTDataKind.Object, false);
        }
    }

    /// <summary>
    /// P0安全数据边界。可变byte[]在进入/离开容器时生成独立副本。
    /// 未知DTO、复杂图像、非托管资源必须待专业适配器提供真实复制/冻结能力。
    /// </summary>
    public static class FDTDataValueSafety
    {
        public static bool IsSimpleImmutable(Type type)
        {
            if (type == null) return false;
            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) type = underlying;
            return type.IsPrimitive || type.IsEnum ||
                type == typeof(string) || type == typeof(decimal) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) || type == typeof(Guid);
        }

        public static object Capture(object value)
        {
            if (value == null) return null;
            Type type = value.GetType();
            if (type == typeof(byte[])) return ((byte[])value).Clone();
            if (IsSimpleImmutable(type)) return value;
            throw new NotSupportedException(
                "类型尚未注册安全复制/冻结能力：" + type.FullName);
        }
    }
}
