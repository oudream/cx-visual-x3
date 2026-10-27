using System;

namespace CxVisual.Foundation.Numerics
{
    /// <summary>纯数值连续RowMajor张量形状与Stride，不包含FDT数据类型/持久化语义。</summary>
    public sealed class FNUTensorShape
    {
        private readonly int[] _dimensions;
        private readonly int[] _strides;

        public FNUTensorShape(params int[] dimensions)
        {
            if (dimensions == null) throw new ArgumentNullException("dimensions");
            if (dimensions.Length == 0) throw new ArgumentException("A tensor must have at least one dimension.", "dimensions");
            _dimensions = (int[])dimensions.Clone();
            _strides = new int[dimensions.Length];
            long count = 1;
            for (int i = 0; i < _dimensions.Length; i++)
            {
                if (_dimensions[i] < 0) throw new ArgumentOutOfRangeException("dimensions");
                count *= _dimensions[i];
                if (count > int.MaxValue) throw new OverflowException("Tensor exceeds int-indexable storage.");
            }
            ElementCount = (int)count;
            int stride = 1;
            for (int i = _dimensions.Length - 1; i >= 0; --i)
            {
                _strides[i] = stride;
                // Zero dimensions are empty; other dimensions still contribute their
                // coordinate stride in a potential sliced view.
                stride = checked(stride * Math.Max(1, _dimensions[i]));
            }
        }

        public int Rank { get { return _dimensions.Length; } }
        public int ElementCount { get; private set; }
        public int DimensionAt(int axis) { return _dimensions[axis]; }
        public int StrideAt(int axis) { return _strides[axis]; }
        public int[] CopyDimensions() { return (int[])_dimensions.Clone(); }
        public int[] CopyStrides() { return (int[])_strides.Clone(); }

        public int Offset(params int[] indices)
        {
            if (indices == null) throw new ArgumentNullException("indices");
            if (indices.Length != Rank) throw new ArgumentException("Index rank mismatch.", "indices");
            int offset = 0;
            for (int i = 0; i < Rank; ++i)
            {
                if (indices[i] < 0 || indices[i] >= _dimensions[i])
                    throw new ArgumentOutOfRangeException("indices");
                offset = checked(offset + indices[i] * _strides[i]);
            }
            return offset;
        }
        public bool HasSameDimensions(FNUTensorShape other)
        {
            if (other == null || other.Rank != Rank) return false;
            for (int i = 0; i < Rank; i++)
                if (other._dimensions[i] != _dimensions[i]) return false;
            return true;
        }
    }
}
