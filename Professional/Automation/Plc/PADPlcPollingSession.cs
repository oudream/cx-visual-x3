using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Professional.Automation.Plc
{
    /// <summary>
    /// 单个PLC设备/点组的宿主所有权采样会话。复用FCRPeriodicJob，不拥有额外的Timer、
    /// Worker、Channel或MDS Current。周期为每轮完成后延时，不宣称硬实时采样周期。
    /// 部分点失败由PADPlcPointBridge写入MDS Stale，下一轮只读重新采样可恢复Good。
    /// 连接断开、点表漂移、协议整批不可信等不可恢复错误终止本会话并交由Host判定重启。
    /// </summary>
    public sealed class PADPlcPollingSession
    {
        private readonly object _gate = new object();
        private readonly PADPlcPointBridge _bridge;
        private readonly string[] _stableIds;
        private readonly TimeSpan _interval;
        private readonly bool _contiguous;
        private FCRPeriodicJob _job;
        private bool _stopped;
        private long _successfulCycles;
        private long _partialCycles;

        public PADPlcPollingSession(PADPlcPointBridge bridge, IEnumerable<string> stableIds,
            TimeSpan interval, bool contiguous = false)
        {
            if (bridge == null) throw new ArgumentNullException("bridge");
            if (stableIds == null) throw new ArgumentNullException("stableIds");
            if (interval < TimeSpan.FromMilliseconds(20) ||
                interval > TimeSpan.FromHours(1))
                throw new ArgumentOutOfRangeException("interval", "PLC轮询周期必须在20ms～1h内");
            string[] points = stableIds.ToArray();
            if (points.Length < 1 || points.Length > 64 ||
                points.Any(string.IsNullOrWhiteSpace) ||
                points.Distinct(StringComparer.Ordinal).Count() != points.Length)
                throw new ArgumentException("周期采集必须包含1～64个唯一稳定点ID", "stableIds");
            _bridge = bridge;
            _stableIds = (string[])points.Clone();
            _interval = interval;
            _contiguous = contiguous;
        }

        public TimeSpan Interval { get { return _interval; } }
        public bool Contiguous { get { return _contiguous; } }
        // 对外表示当前可继续采样的会话，不是“历史上曾启动”。
        // 故障完成或Host发出Stop后立即不再对外宣称Running。
        public bool IsStarted
        {
            get
            {
                lock (_gate)
                    return _job != null && !_stopped && !_job.Completion.IsCompleted;
            }
        }
        public long CompletedCycles
        {
            get
            {
                lock (_gate) return _job == null ? 0 : _job.Executions;
            }
        }
        public long SuccessfulCycles { get { return Interlocked.Read(ref _successfulCycles); } }
        public long PartialCycles { get { return Interlocked.Read(ref _partialCycles); } }
        public Task Completion
        {
            get
            {
                lock (_gate)
                {
                    if (_job == null) throw new InvalidOperationException("PLC周期会话尚未启动");
                    return _job.Completion;
                }
            }
        }

        /// <summary>宿主显式启动；重复启动、已停止会话都拒绝。错误通过Completion传递。</summary>
        public void Start(bool runImmediately = true,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (_gate)
            {
                if (_stopped) throw new InvalidOperationException("PLC周期会话已经停止，不可重启旧会话");
                if (_job != null) throw new InvalidOperationException("PLC周期会话已在运行");
                cancellationToken.ThrowIfCancellationRequested();
                _job = new FCRPeriodicJob(_interval, ScanCycleAsync, runImmediately, cancellationToken);
            }
        }

        private async Task ScanCycleAsync(CancellationToken cancellationToken)
        {
            PADPlcScanResult result = _contiguous
                ? await _bridge.ScanContiguousAsync(_stableIds, cancellationToken).ConfigureAwait(false)
                : await _bridge.ScanAsync(_stableIds, cancellationToken).ConfigureAwait(false);
            // 仅保存运行计数，不复制上一轮采集值；数据唯一Owner始终为MDS。
            if (result.AllSucceeded) Interlocked.Increment(ref _successfulCycles);
            else Interlocked.Increment(ref _partialCycles);
        }

        /// <summary>
        /// 取消下一轮、协作等待在途读取停止；不强杀Channel/PLC，也不自发重连。
        /// 如果在途执行因设备断线/点表漂移而异常，向调用方传播原故障。
        /// </summary>
        public Task StopAsync()
        {
            FCRPeriodicJob job;
            lock (_gate)
            {
                _stopped = true;
                job = _job;
            }
            return job == null ? Task.FromResult(true) : job.StopAsync();
        }
    }
}
