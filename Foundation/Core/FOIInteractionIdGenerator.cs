using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Core
{
    /// <summary>正long雪花码；进程内Worker排他，跨进程的Worker唯一分配须由部署侧保障。</summary>
    public sealed class FOIInteractionIdGenerator : IDisposable
    {
        private sealed class State
        {
            internal long Last = -1;
            internal int Sequence;
            internal bool Leased;
        }
        private static readonly object Gate = new object();
        private static readonly Dictionary<int, State> States = new Dictionary<int, State>();
        private static readonly long Epoch = new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc).Ticks/TimeSpan.TicksPerMillisecond;
        private readonly State _state;
        private readonly int _worker;
        private readonly Func<long> _clock;
        private bool _disposed;
        public FOIInteractionIdGenerator(int worker) : this(worker, () => DateTime.UtcNow.Ticks/TimeSpan.TicksPerMillisecond) {}
        public FOIInteractionIdGenerator(int worker, Func<long> utcMilliseconds)
        {
            if(worker<0||worker>1023) throw new ArgumentOutOfRangeException("worker");
            if(utcMilliseconds==null) throw new ArgumentNullException("utcMilliseconds");
            _worker=worker;_clock=utcMilliseconds;
            lock(Gate)
            {
                State state;
                if(!States.TryGetValue(worker,out state)){state=new State();States.Add(worker,state);}
                if(state.Leased) throw new InvalidOperationException("WorkerId已被本进程占用");
                state.Leased=true;_state=state;
            }
        }
        public long NextId()
        {
            lock(Gate)
            {
                if(_disposed) throw new ObjectDisposedException("FOIInteractionIdGenerator");
                long ms=_clock()-Epoch;
                if(ms<0||ms>=(1L<<41)) throw new InvalidOperationException("时间超出雪花码范围");
                if(ms<_state.Last) throw new InvalidOperationException("时钟回拨");
                if(ms==_state.Last)
                {
                    if(_state.Sequence==4095) throw new InvalidOperationException("同毫秒序号耗尽");
                    ++_state.Sequence;
                }
                else{_state.Last=ms;_state.Sequence=0;}
                return (ms<<22)|((long)_worker<<12)|(uint)_state.Sequence;
            }
        }
        public void Dispose(){lock(Gate){if(_disposed)return;_disposed=true;_state.Leased=false;}}
    }
}
