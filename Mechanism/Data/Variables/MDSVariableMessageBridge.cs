using System;
using System.Collections.Generic;
using System.Threading;
using CxVisual.Foundation.Data.Messages;
using CxVisual.Mechanism.Data.Messages;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>对外公布已提交变量状态变化的轻量通知，不复制图像/体数据，也不传入另一份可写值仓。</summary>
    public sealed class MDSVariableChangeNotice
    {
        public MDSVariableChangeNotice(string scopeId, MDSVariableChange change)
        {
            ScopeId = scopeId; VariableId = change.VariableId; Name = change.Name;
            Revision = change.Revision; Kind = change.Kind; Quality = change.Quality;
            Source = change.Source; UpdatedUtc = change.UpdatedUtc;
        }
        public string ScopeId { get; private set; }
        public string VariableId { get; private set; }
        public string Name { get; private set; }
        public long Revision { get; private set; }
        public MDSVariableChangeKind Kind { get; private set; }
        public MDSVariableQuality Quality { get; private set; }
        public string Source { get; private set; }
        public DateTime UpdatedUtc { get; private set; }
    }

    /// <summary>
    /// 变量Change -> 已有MNMMessageCenter的可选适配，不新增Broker、RTDB或第二消息总线。
    /// 内部采用LatestOnly订阅；逐笔可靠采集应使用明确的专业Stream而非本桥接器。
    /// </summary>
    public sealed class MDSVariableMessageBridge : IDisposable
    {
        private readonly MNMMessageCenter _messages;
        private readonly MDSVariableSubscription _subscription;
        private readonly string _scope;
        private readonly string _prefix;
        private long _published, _failed;
        private Exception _lastError;

        public MDSVariableMessageBridge(MDSVariableTable table,
            MDSVariableChangeDispatcher dispatcher, MNMMessageCenter messages,
            string topicPrefix = "/machine", IEnumerable<string> variableIds = null)
        {
            if (table == null || dispatcher == null || messages == null)
                throw new ArgumentNullException("table/dispatcher/messages");
            if (string.IsNullOrWhiteSpace(topicPrefix)) throw new ArgumentException("Topic不能为空");
            _scope = table.ScopeId;
            _prefix = topicPrefix.TrimEnd('/');
            // M36的Topic身份是八段路径；这里仅允许machine/container根，不自造协议。
            if (_prefix != "/machine" && _prefix != "/container")
                throw new ArgumentException("变量消息桥只接受M36的/machine或/container根", "topicPrefix");
            MNMTopicFilter.ValidateTopic(_prefix + "/" + _scope +
                "/app/CxVisual/module/Variables/message/Example");
            _messages = messages;
            _subscription = dispatcher.Subscribe(OnChanges, variableIds);
        }
        private void OnChanges(IReadOnlyList<MDSVariableChange> batch)
        {
            foreach (MDSVariableChange change in batch)
            {
                try
                {
                    string topic = _prefix + "/" + _scope +
                        "/app/CxVisual/module/Variables/message/" + change.VariableId;
                    // 不偷偷编码或改写VariableId；非法ID必须显式记失败。
                    MNMTopicFilter.ValidateTopic(topic);
                    MNMPublishReceipt receipt = _messages.Publish(topic,
                        new MDSVariableChangeNotice(_scope, change),
                        new MNMPublishOptions { SourceId = "CxVisual.MDS.Variables",
                            DispatchMode = MNMDispatchMode.Inline });
                    if (!receipt.Accepted) throw new InvalidOperationException("消息中心拒绝变量通知");
                    Interlocked.Increment(ref _published);
                }
                catch (Exception error)
                {
                    _lastError = error;
                    Interlocked.Increment(ref _failed);
                }
            }
        }
        public long Published { get { return Interlocked.Read(ref _published); } }
        public long Failed { get { return Interlocked.Read(ref _failed); } }
        public Exception LastError { get { return _lastError; } }
        public void Dispose() { _subscription.Dispose(); }
    }
}
