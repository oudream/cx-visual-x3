using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Mechanism.Runtime.Commands;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 命令人工页的多Operation投影，仅记录真实MRK响应，不创建运行真源或持久审计仓。
    /// 已提交操作由原Host的唯一账本负责，表格只保存有界的可见快照。
    /// </summary>
    public sealed partial class TCTManualCommandFrame
    {
        private sealed class TrackedCommandOperation
        {
            public Guid OperationId { get; set; }
            public string Target { get; set; }
            public string CommandCode { get; set; }
            public string Endpoint { get; set; }
            public bool Remote { get; set; }
            public string State { get; set; }
            public string Stage { get; set; }
            public string Progress { get; set; }
            public string ErrorCode { get; set; }
            public string Started { get; set; }
        }

        private readonly ObservableCollection<TrackedCommandOperation> _trackedOperations =
            new ObservableCollection<TrackedCommandOperation>();
        private readonly DataGrid _operationTable = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(4) };
        private readonly ComboBox _batchSize = new ComboBox { Width = 100, Height = 30,
            Margin = new Thickness(6) };
        public int TrackedOperationCount { get { return _trackedOperations.Count; } }
        public Guid[] TrackedOperationIds
        { get { return _trackedOperations.Select(r => r.OperationId).ToArray(); } }

        private void BuildMultipleOperationPanel(TabControl tabs)
        {
            foreach (int count in new[] { 2, 4, 8, 16, 32 })
                _batchSize.Items.Add(count);
            _batchSize.SelectedIndex = 1;
            StackPanel root = new StackPanel { Margin = new Thickness(10) };
            root.Children.Add(new TextBlock { Text = "多 Operation 运行观察",
                FontSize = 19, FontWeight = FontWeights.SemiBold });
            root.Children.Add(Caption("当前工作区记录真实MRK Operation快照，最多250项；选择一行后可通过既有Query/暂停/恢复/取消操作。失败与Unknown不自动重发。"));
            StackPanel tools = Row();
            tools.Children.Add(Caption("批量数量（2个并发槽位，分波执行）"));
            tools.Children.Add(_batchSize);
            Button run = new Button { Content = "执行本地Multiply批量", Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(5) };
            AutomationProperties.SetAutomationId(run, "TCT.Command.Batch.Run");
            run.Click += (s, e) => Run(() => BatchRunAsync());
            tools.Children.Add(run);
            root.Children.Add(tools);
            _operationTable.ItemsSource = _trackedOperations;
            _operationTable.Columns.Add(new DataGridTextColumn { Header = "时间",
                Binding = new System.Windows.Data.Binding("Started"), Width = new DataGridLength(110) });
            _operationTable.Columns.Add(new DataGridTextColumn { Header = "OperationId",
                Binding = new System.Windows.Data.Binding("OperationId"), Width = new DataGridLength(260) });
            _operationTable.Columns.Add(new DataGridTextColumn { Header = "目标/命令",
                Binding = new System.Windows.Data.Binding("CommandCode"), Width = new DataGridLength(140) });
            _operationTable.Columns.Add(new DataGridTextColumn { Header = "Stage",
                Binding = new System.Windows.Data.Binding("Stage"), Width = new DataGridLength(95) });
            _operationTable.Columns.Add(new DataGridTextColumn { Header = "状态",
                Binding = new System.Windows.Data.Binding("State"), Width = new DataGridLength(112) });
            _operationTable.Columns.Add(new DataGridTextColumn { Header = "进度",
                Binding = new System.Windows.Data.Binding("Progress"), Width = new DataGridLength(85) });
            _operationTable.Columns.Add(new DataGridTextColumn { Header = "错误码",
                Binding = new System.Windows.Data.Binding("ErrorCode"), Width = new DataGridLength(180) });
            _operationTable.SelectionChanged += (s, e) =>
            {
                TrackedCommandOperation item = _operationTable.SelectedItem as TrackedCommandOperation;
                if (item == null) return;
                _activeOperation = item.OperationId;
                _operationRemote = item.Remote;
                _operationEndpoint = item.Endpoint;
                Show("已选择Operation " + item.OperationId +
                    "，使用原执行端点进行查询；禁止跨Host误操作。");
            };
            AutomationProperties.SetAutomationId(_operationTable, "TCT.Command.Operations");
            root.Children.Add(_operationTable);
            tabs.Items.Add(Tab("多Operation管理", root));
        }

        private void TrackSubmission(MRKCommandRequest request)
        {
            if (request == null || _closed) return;
            if (_trackedOperations.Any(x => x.OperationId == request.OperationId)) return;
            _trackedOperations.Add(new TrackedCommandOperation {
                OperationId = request.OperationId, Target = request.Target,
                CommandCode = request.CommandCode, Remote = request.Options.Remote,
                Endpoint = request.Options.Remote ? _host.Text + "|" + _pipe.Text : null,
                State = "Requested", Stage = "Submitted", Progress = "未报告",
                ErrorCode = string.Empty, Started = DateTime.Now.ToString("HH:mm:ss.fff")
            });
            while (_trackedOperations.Count > 250) _trackedOperations.RemoveAt(0);
        }

        private void TrackResponse(MRKCommandResponse response)
        {
            if (_closed || response == null) return;
            TrackedCommandOperation row = _trackedOperations.FirstOrDefault(
                x => x.OperationId == response.OperationId);
            if (row == null) return;
            row.Stage = response.Stage.ToString();
            row.State = response.State.ToString();
            row.ErrorCode = response.ErrorCode ?? string.Empty;
            row.Progress = response.ProgressPercent.HasValue ?
                response.ProgressPercent.Value.ToString("F1", CultureInfo.InvariantCulture) + "%" :
                "未报告";
            _operationTable.Items.Refresh();
        }

        private async Task BatchRunAsync()
        {
            int count = (int)_batchSize.SelectedItem;
            await BatchRunAsync(count);
        }

        /// <summary>真实MRK InvokeBatchAsync；分波保证演示Provider的两个并发槽位。</summary>
        public async Task<IReadOnlyList<MRKCommandResponse>> BatchRunAsync(int count)
        {
            if (_closed) throw new ObjectDisposedException("TCTManualCommandFrame");
            if (_busy) throw new InvalidOperationException("已有命令等待返回，请完成后再执行批量");
            if (IsRemote)
                throw new NotSupportedException("本页批量操作仅限本地演示Provider；跨Host目标需显式授权组");
            if (count < 1 || count > 32) throw new ArgumentOutOfRangeException("count");
            MRKCommandDescriptor descriptor = _local.Router.Discover()
                .FirstOrDefault(x => x.Definition.CommandCode == "Multiply" &&
                    x.Target == "TestCenter/Algorithm");
            if (descriptor == null) throw new InvalidOperationException("本地倍乘Provider未注册或禁用");
            _busy = true; Changed();
            List<MRKCommandResponse> results = new List<MRKCommandResponse>();
            try
            {
                for (int start = 0; start < count; start += 2)
                {
                    List<MRKCommandRequest> wave = new List<MRKCommandRequest>();
                    for (int n = start; n < Math.Min(start + 2, count); ++n)
                    {
                        MRKCommandRequest request = new MRKCommandRequest(descriptor.Target,
                            descriptor.Definition.CommandCode, new Dictionary<string, object> {
                                { "Input", (double)(n + 1) }, { "Factor", 2.0 }
                            }, caller: "TestCenter.Manual.Batch",
                            expectedSchemaVersion: descriptor.Definition.SchemaVersion,
                            commandId: descriptor.Definition.CommandId);
                        TrackSubmission(request);
                        wave.Add(request);
                    }
                    IReadOnlyList<MRKCommandResponse> settled =
                        await _local.InvokeBatchAsync(wave).ConfigureAwait(true);
                    foreach (MRKCommandResponse response in settled)
                    {
                        results.Add(response);
                        TrackResponse(response);
                        Log("Batch", response.OperationId,
                            response.Stage + "/" + response.State,
                            response.ErrorCode + " / " + response.Outputs.Count + "个输出");
                    }
                }
                Show("已执行 " + count + " 个真实MRK批量Operation（逐个状态见表格）");
                return results.AsReadOnly();
            }
            finally { _busy = false; Changed(); }
        }
    }
}
