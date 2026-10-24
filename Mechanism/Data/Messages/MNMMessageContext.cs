using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Messages;

namespace CxVisual.Mechanism.Data.Messages
{
    public sealed class MNMMessageContext<T>
    {
        private readonly MNMMessageCenter _owner;
        public MNMMessage Message { get; private set; }
        public MNMMessageHeader Header { get { return Message.Header; } }
        public T Payload { get { return Message.Payload.Get<T>(); } }
        internal MNMMessageContext(MNMMessageCenter owner, MNMMessage message)
        { _owner = owner; Message = message; }
        /// <summary>回复自动保持CorrelationId；消息回复不是M12命令Handler的执行确认。</summary>
        public Task<MNMPublishReceipt> ReplyAsync<TReply>(TReply response,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(Header.ReplyTo))
                throw new InvalidOperationException("原消息没有ReplyTo");
            return _owner.PublishAsync(Header.ReplyTo, response,
                new MNMPublishOptions {
                    CorrelationId = Header.CorrelationId,
                    DispatchMode = MNMDispatchMode.Inline
                },
                cancellationToken);
        }
    }

    public interface IMNMMessageSubscriber
    {
        IDisposable Subscribe<T>(string topicFilter, Action<MNMMessageContext<T>> handler,
            MNMSubscriptionOptions options = null);
        IDisposable SubscribeAsync<T>(string topicFilter, Func<MNMMessageContext<T>,Task> handler,
            MNMSubscriptionOptions options = null);
    }
}
