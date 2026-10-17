using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>
    /// 将普通C#算法以强类型输入输出绑定到已存在的DataContext。
    /// 不产生第二个执行器、调度器或独立端口注册表，异常由MRK运行器原样处理。
    /// </summary>
    public static class MDSDataFunctions
    {
        public static TOutput Invoke<TInput,TOutput>(IMDSDataContext data,
            FDTDataKey<TInput> input, FDTDataKey<TOutput> output,
            Func<TInput,TOutput> algorithm)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (input == null) throw new ArgumentNullException("input");
            if (output == null) throw new ArgumentNullException("output");
            if (algorithm == null) throw new ArgumentNullException("algorithm");
            TOutput result = algorithm(data.Get(input));
            data.Return(output, result);
            return result;
        }

        public static async Task<TOutput> InvokeAsync<TInput,TOutput>(IMDSDataContext data,
            FDTDataKey<TInput> input, FDTDataKey<TOutput> output,
            Func<TInput,CancellationToken,Task<TOutput>> algorithm,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (data == null) throw new ArgumentNullException("data");
            if (input == null) throw new ArgumentNullException("input");
            if (output == null) throw new ArgumentNullException("output");
            if (algorithm == null) throw new ArgumentNullException("algorithm");
            cancellation.ThrowIfCancellationRequested();
            TOutput result = await algorithm(data.Get(input), cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            data.Return(output, result);
            return result;
        }
    }
}
