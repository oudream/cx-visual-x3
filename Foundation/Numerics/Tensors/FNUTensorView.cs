using System;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>
    /// Live, read-only zero-data-copy tensor view. Holds a reference to the owning
    /// buffer; owner.Set changes are visible. Callers synchronize concurrent writes.
    /// Metadata is owned by the view; only CopyToBuffer creates independent data.
    /// </summary>
    public sealed class FNUTensorView
    {
        private readonly FNUTensorBuffer _owner;
        private readonly int[] _dimensions, _strides;
        private readonly long _offset;

        internal FNUTensorView(FNUTensorBuffer owner, int[] dimensions, int[] strides, long offset)
        {
            if (owner == null) throw new ArgumentNullException("owner");
            if (dimensions == null) throw new ArgumentNullException("dimensions");
            if (strides == null || dimensions.Length == 0 || dimensions.Length != strides.Length)
                throw new ArgumentException("Tensor view rank mismatch.", "strides");
            _owner = owner;
            _dimensions = (int[])dimensions.Clone();
            _strides = (int[])strides.Clone();
            _offset = offset;
            bool empty = false;
            for (int i = 0; i < _dimensions.Length; ++i)
            {
                if (_dimensions[i] < 0 || _strides[i] < 0)
                    throw new ArgumentOutOfRangeException("dimensions");
                if (_dimensions[i] == 0) empty = true;
            }
            long count = empty ? 0 : 1;
            if (!empty)
                for (int i = 0; i < _dimensions.Length; ++i)
                {
                    if (count > int.MaxValue / _dimensions[i])
                        throw new OverflowException("Tensor view size exceeds Int32.");
                    count *= _dimensions[i];
                }
            ElementCount = (int)count;
        }

        internal FNUTensorBuffer Owner { get { return _owner; } }
        public int Rank { get { return _dimensions.Length; } }
        public int ElementCount { get; private set; }
        public int DimensionAt(int axis) { return _dimensions[axis]; }
        public int StrideAt(int axis) { return _strides[axis]; }
        public int[] CopyDimensions() { return (int[])_dimensions.Clone(); }
        public int[] CopyStrides() { return (int[])_strides.Clone(); }

        public double Get(params int[] indices)
        {
            if (indices == null) throw new ArgumentNullException("indices");
            if (indices.Length != Rank) throw new ArgumentException("Tensor view rank mismatch.", "indices");
            long address = _offset;
            for (int i = 0; i < Rank; ++i)
            {
                if (indices[i] < 0 || indices[i] >= _dimensions[i])
                    throw new ArgumentOutOfRangeException("indices");
                address += (long)indices[i] * _strides[i];
            }
            return ReadAt(address);
        }

        /// <summary>Allocation-free per-access 2D index lookup.</summary>
        public double Get2D(int row, int column)
        {
            if (Rank != 2) throw new InvalidOperationException("View must have two axes.");
            if (row < 0 || row >= _dimensions[0] ||
                column < 0 || column >= _dimensions[1])
                throw new ArgumentOutOfRangeException("row");
            return ReadAt(_offset + (long)row * _strides[0] + (long)column * _strides[1]);
        }

        /// <summary>Read a logical row-major position even when physical strides differ.</summary>
        public double GetFlat(int index)
        {
            if (index < 0 || index >= ElementCount) throw new ArgumentOutOfRangeException("index");
            long address = _offset;
            for (int axis = Rank - 1; axis >= 0; --axis)
            {
                int coordinate = index % _dimensions[axis];
                index /= _dimensions[axis];
                address += (long)coordinate * _strides[axis];
            }
            return ReadAt(address);
        }

        /// <summary>Half-open range, sharing the original owner.</summary>
        public FNUTensorView Slice(int axis, int start, int length)
        {
            if (axis < 0 || axis >= Rank) throw new ArgumentOutOfRangeException("axis");
            if (start < 0 || length < 0 || start > _dimensions[axis] ||
                length > _dimensions[axis] - start)
                throw new ArgumentOutOfRangeException("length");
            var dims = CopyDimensions();
            dims[axis] = length;
            return new FNUTensorView(_owner, dims, _strides,
                _offset + (long)start * _strides[axis]);
        }

        public FNUTensorView Transpose(int firstAxis, int secondAxis)
        {
            if (firstAxis < 0 || firstAxis >= Rank) throw new ArgumentOutOfRangeException("firstAxis");
            if (secondAxis < 0 || secondAxis >= Rank) throw new ArgumentOutOfRangeException("secondAxis");
            var dims = CopyDimensions();
            var strides = CopyStrides();
            int d = dims[firstAxis]; dims[firstAxis] = dims[secondAxis]; dims[secondAxis] = d;
            int s = strides[firstAxis]; strides[firstAxis] = strides[secondAxis]; strides[secondAxis] = s;
            return new FNUTensorView(_owner, dims, strides, _offset);
        }

        /// <summary>Materialize an independent contiguous tensor snapshot.</summary>
        public FNUTensorBuffer CopyToBuffer()
        {
            var result = new FNUTensorBuffer(new FNUTensorShape(CopyDimensions()));
            if (ElementCount == 0) return result;
            // Carry multi-axis coordinates instead of dividing the logical flat
            // index by every dimension for each sample.
            var coordinates = new int[Rank];
            long address = _offset;
            for (int index = 0; index < ElementCount; ++index)
            {
                result.Put(index, ReadAt(address));
                if (index + 1 < ElementCount) Advance(coordinates, ref address);
            }
            return result;
        }

        /// <summary>
        /// Numerically compensated sum over a strided view. One coordinate
        /// array per invocation, no allocation or integer divisions per sample.
        /// </summary>
        internal double SumCompensated()
        {
            if (ElementCount == 0) return 0;
            var coordinates = new int[Rank];
            long address = _offset;
            double sum = 0, correction = 0;
            for (int index = 0; index < ElementCount; ++index)
            {
                double value = ReadAt(address);
                double next = sum + value;
                correction += Math.Abs(sum) >= Math.Abs(value)
                    ? (sum - next) + value : (value - next) + sum;
                sum = next;
                if (index + 1 < ElementCount) Advance(coordinates, ref address);
            }
            double result = sum + correction;
            if (double.IsNaN(result) || double.IsInfinity(result))
                throw new OverflowException("View reduction overflow.");
            return result;
        }

        private void Advance(int[] coordinates, ref long address)
        {
            // Amortized O(1): the innermost axis is advanced most frequently.
            for (int axis = Rank - 1; axis >= 0; --axis)
            {
                coordinates[axis]++;
                address += _strides[axis];
                if (coordinates[axis] < _dimensions[axis]) return;
                address -= (long)coordinates[axis] * _strides[axis];
                coordinates[axis] = 0;
            }
        }

        private double ReadAt(long index)
        {
            if (index < 0 || index >= _owner.Shape.ElementCount)
                throw new InvalidOperationException("View address outside its backing buffer.");
            return _owner.At((int)index);
        }
    }
}
