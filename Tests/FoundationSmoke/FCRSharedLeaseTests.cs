using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
namespace CxVisual.Tests.FoundationSmoke
{
    internal static class FCRSharedLeaseTests
    {
        internal static void Run()
        {
            int released = 0;
            byte[] data = new byte[1024];
            FCRSharedLease<byte[]> owner = new FCRSharedLease<byte[]>(data,
                value => Interlocked.Increment(ref released));
            FCRSharedLease<byte[]> left = owner.Retain();
            FCRSharedLease<byte[]> right = owner.Retain();
            owner.Dispose();
            if (released != 0) throw new InvalidOperationException("共享图像提前释放");
            Task a = Task.Run(() => { if (left.Value.Length != 1024) throw new Exception(); left.Dispose(); });
            Task b = Task.Run(() => { if (right.Value.Length != 1024) throw new Exception(); right.Dispose(); });
            Task all = Task.WhenAll(a, b);
            if (!object.ReferenceEquals(Task.WhenAny(all, Task.Delay(8000)).GetAwaiter().GetResult(), all))
                throw new TimeoutException("分支未清理");
            all.GetAwaiter().GetResult();
            if (released != 1) throw new InvalidOperationException("释放次数不为1");
            right.Dispose();
            bool invalid = false;
            try { right.Retain(); } catch (ObjectDisposedException) { invalid = true; }
            if (!invalid) throw new InvalidOperationException("已释放票据仍可引用");
        }
    }
}
