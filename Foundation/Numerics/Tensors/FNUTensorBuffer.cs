using System;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>双精度、独占连续RowMajor存储；构造拷贝输入，公开读取也拷贝。无业务数据身份。</summary>
    public sealed class FNUTensorBuffer
    {
        private readonly double[] _values;
        public FNUTensorShape Shape { get; private set; }

        public FNUTensorBuffer(FNUTensorShape shape)
        {
            if (shape == null) throw new ArgumentNullException("shape");
            Shape = shape;
            _values = new double[shape.ElementCount];
        }
        public FNUTensorBuffer(FNUTensorShape shape, double[] data)
        {
            if (shape == null) throw new ArgumentNullException("shape");
            if (data == null) throw new ArgumentNullException("data");
            if (shape.ElementCount != data.Length)
                throw new ArgumentException("Tensor data length differs from shape.", "data");
            for (int i = 0; i < data.Length; i++) RequireFinite(data[i]);
            Shape = shape;
            _values = (double[])data.Clone();
        }

        public double Get(params int[] indices) { return _values[Shape.Offset(indices)]; }
        public void Set(double value, params int[] indices)
        {
            RequireFinite(value);
            _values[Shape.Offset(indices)] = value;
        }
        public double[] CopyValues() { return (double[])_values.Clone(); }
        /// <summary>Creates a live read-only view without copying tensor data.</summary>
        public FNUTensorView AsView()
        {
            return new FNUTensorView(this, Shape.CopyDimensions(), Shape.CopyStrides(), 0);
        }

        /// <summary>改变连续存储的形状，返回新的独占缓冲区，不共享原数据。</summary>
        public FNUTensorBuffer Reshape(FNUTensorShape newShape)
        {
            if (newShape == null) throw new ArgumentNullException("newShape");
            if (newShape.ElementCount != _values.Length)
                throw new ArgumentException("Reshape must preserve element count.", "newShape");
            return new FNUTensorBuffer(newShape, _values);
        }
        internal double At(int offset) { return _values[offset]; }
        internal void Put(int offset, double value) { RequireFinite(value); _values[offset] = value; }

        /// <summary>
        /// 复制指定轴的半开区间切片。结果拥有独立缓冲区。
        /// 使用View逻辑游标遍历，避免为每个元素重算多维商与余数。
        /// </summary>
        public FNUTensorBuffer Slice(int axis, int start, int length)
        {
            return AsView().Slice(axis, start, length).CopyToBuffer();
        }

        private static void RequireFinite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException("value", "Tensor values must be finite.");
        }
    }
}
