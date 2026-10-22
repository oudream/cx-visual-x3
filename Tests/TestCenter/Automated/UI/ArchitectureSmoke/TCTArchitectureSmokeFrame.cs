using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// One UI frame for invoking the original ArchitectureSmoke EXE.
    /// Local invocation status is never promoted to a TestCenter Attempt or Golden.
    /// </summary>
    public sealed class TCTArchitectureSmokeFrame : UserControl, ITCTWorkspaceFrame
    {
        private const int MaxDisplayCharacters = 150000;
        private readonly TCTArchitectureSmokeProcessRunner _runner =
            new TCTArchitectureSmokeProcessRunner();
        private readonly ComboBox _scope = new ComboBox {
            MinWidth = 230, MaxWidth = 430, Margin = new Thickness(0, 8, 0, 9),
            DisplayMemberPath = "Title"
        };
        private readonly TextBlock _state = new TextBlock {
            FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateBlue,
            Margin = new Thickness(0, 8, 0, 7)
        };
        private readonly TextBlock _result = new TextBlock {
            Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 9)
        };
        private readonly TextBox _program = new TextBox {
            IsReadOnly = true, BorderThickness = new Thickness(0),
            Background = Brushes.AliceBlue, Foreground = Brushes.DarkSlateGray,
            FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        private readonly TextBox _output = new TextBox {
            IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(252, 253, 255)),
            Foreground = Brushes.DarkSlateGray, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderBrush = Brushes.LightSteelBlue, BorderThickness = new Thickness(1)
        };
        private CancellationTokenSource _cancel;
        private bool _running;
        private bool _closed;
        private int _messageCount;
        private int _runGeneration;
        private bool _truncated;

        public string WorkspaceId { get { return "auto.architecture-smoke"; } }
        public bool IsRunning { get { return _running; } }
        public TCTArchitectureSmokeRunResult LastResult { get; private set; }
        public int MessageCount { get { return _messageCount; } }
        public string CurrentProfileId
        {
            get { return (_scope.SelectedItem as TCTArchitectureSmokeProfile)?.Id; }
        }
        public event Action StateChanged;
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get {
                return new[] {
                    new TCTWorkspaceAction("architecture.run", "运行当前范围",
                        StartSelected, () => !_closed && !_running && File.Exists(_runner.ExecutablePath)),
                    new TCTWorkspaceAction("architecture.stop", "停止执行",
                        CancelCurrent, () => !_closed && _running),
                    new TCTWorkspaceAction("architecture.clear", "清空本页输出",
                        ClearOutput, () => !_closed && !_running && _output.Text.Length > 0)
                };
            }
        }

        public TCTArchitectureSmokeFrame()
        {
            Background = Brushes.White;
            _scope.ItemsSource = TCTArchitectureSmokeProfiles.All;
            _scope.SelectedIndex = 0;
            _scope.SelectionChanged += (sender, args) => UpdateSelected();

            DockPanel page = new DockPanel { Margin = new Thickness(22, 16, 20, 14) };
            StackPanel header = new StackPanel { Margin = new Thickness(0, 0, 0, 11) };
            header.Children.Add(new TextBlock {
                Text = "ArchitectureSmoke / 架构回归", FontSize = 22,
                FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(36, 68, 103))
            });
            header.Children.Add(new TextBlock {
                Text = "调用已存在的独立测试程序。测试逻辑及CLI参数不变；本页只观察该进程的真实输出和退出码。",
                Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 8)
            });
            header.Children.Add(new TextBlock { Text = "执行范围 / 固定命令预设", FontWeight = FontWeights.SemiBold });
            header.Children.Add(_scope);
            header.Children.Add(new TextBlock { Text = "将调用的真实程序", FontWeight = FontWeights.SemiBold });
            header.Children.Add(_program);
            header.Children.Add(_state);
            header.Children.Add(_result);
            header.Children.Add(new TextBlock {
                Text = "结果判定：ExitCode=0 只代表该进程的整体执行成功，不代表获得逐条 NUnit/TCT Attempt；未编译、取消、超时不得显示通过。",
                Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap
            });
            DockPanel.SetDock(header, Dock.Top);
            page.Children.Add(header);
            Border logCard = new Border { Background = Brushes.White,
                BorderBrush = Brushes.LightSteelBlue, BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 10) };
            DockPanel logLayout = new DockPanel();
            TextBlock heading = new TextBlock {
                Text = "标准输出 / 错误输出 · 实时进程记录（UTC）",
                Foreground = Brushes.DarkSlateBlue, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(1, 0, 0, 7)
            };
            DockPanel.SetDock(heading, Dock.Top);
            logLayout.Children.Add(heading);
            logLayout.Children.Add(_output);
            logCard.Child = logLayout;
            page.Children.Add(logCard);
            Content = page;
            AutomationProperties.SetAutomationId(this, "TCT.ArchitectureSmoke.Frame");
            AutomationProperties.SetAutomationId(_scope, "TCT.ArchitectureSmoke.Profile");
            AutomationProperties.SetAutomationId(_state, "TCT.ArchitectureSmoke.State");
            AutomationProperties.SetAutomationId(_result, "TCT.ArchitectureSmoke.Result");
            AutomationProperties.SetAutomationId(_output, "TCT.ArchitectureSmoke.Output");
            _runner.OutputReceived += OnProcessOutput;
            UpdateSelected();
        }

        public void SelectProfile(string id)
        {
            if (_running) throw new InvalidOperationException("Cannot change mode while running");
            TCTArchitectureSmokeProfile profile = TCTArchitectureSmokeProfiles.Find(id);
            if (profile == null) throw new ArgumentException("Unknown profile", "id");
            _scope.SelectedItem = profile;
        }
        private void UpdateSelected()
        {
            TCTArchitectureSmokeProfile p = _scope.SelectedItem as TCTArchitectureSmokeProfile;
            if (p == null) return;
            _program.Text = _runner.ExecutablePath + (p.Arguments.Length == 0 ? "" : " " + p.Arguments);
            _state.Text = _running ? "运行中 · " + p.Title :
                (File.Exists(_runner.ExecutablePath) ? "就绪 · " + p.Title :
                 "未构建 / Unsupported：未找到 ArchitectureSmoke.exe");
            if (!_running && LastResult == null)
                _result.Text = File.Exists(_runner.ExecutablePath) ?
                    "等待运行；ArchitectureSmoke 可继续从命令行单独执行。" :
                    "请先构建 CxVisual.Unified.sln 中原有 ArchitectureSmoke 项目。";
            FireChanged();
        }

        private void OnProcessOutput(string line, bool stderr)
        {
            if (_closed) return;
            int generation = Volatile.Read(ref _runGeneration);
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                // Async stdout events can arrive just before exit but render after
                // the exit result. Preserve those real lines, without mixing runs.
                if (_closed || generation != _runGeneration || _truncated) return;
                if (_messageCount >= 2500) { _truncated = true; return; }
                string entry = DateTime.UtcNow.ToString("HH:mm:ss.fff") +
                    (stderr ? " [stderr] " : " [stdout] ") + line + Environment.NewLine;
                if (_output.Text.Length + entry.Length > MaxDisplayCharacters)
                {
                    _truncated = true;
                    _output.AppendText("[View limit reached; output truncated in this UI]" + Environment.NewLine);
                    return;
                }
                _messageCount++;
                _output.AppendText(entry);
                _output.ScrollToEnd();
            }));
        }

        private async void StartSelected()
        {
            if (_closed || _running) return;
            TCTArchitectureSmokeProfile p = _scope.SelectedItem as TCTArchitectureSmokeProfile;
            if (p == null) return;
            if (!File.Exists(_runner.ExecutablePath))
            {
                UpdateSelected();
                return;
            }
            _cancel = new CancellationTokenSource();
            _running = true;
            Interlocked.Increment(ref _runGeneration);
            _truncated = false;
            _scope.IsEnabled = false;
            LastResult = null;
            _messageCount = 0;
            _output.Clear();
            _state.Text = "运行中 / Running · " + p.Title;
            _result.Text = "启动 UTC：" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                " · 超时上限：" + p.Timeout.TotalSeconds + " 秒";
            FireChanged();
            try
            {
                TCTArchitectureSmokeRunResult result =
                    await _runner.RunAsync(p, _cancel.Token);
                if (_closed) return;
                LastResult = result;
                _state.Text = result.Succeeded ? "已完成 / ExitCode 0" :
                    result.Cancelled ? "已取消" : result.TimedOut ? "已超时" :
                    "执行失败 / Failed";
                _state.Foreground = result.Succeeded ? Brushes.SeaGreen : Brushes.Firebrick;
                _result.Text = "退出码：" +
                    (result.ExitCode.HasValue ? result.ExitCode.Value.ToString() : "无") +
                    " · 耗时：" + result.Elapsed.TotalSeconds.ToString("F2") + " 秒" +
                    " · " + (result.Succeeded ? "进程执行成功（非逐项测试Passed）" :
                        result.Cancelled ? "用户取消" : result.TimedOut ? "超时，已终止子进程" :
                        (result.Failure ?? "进程非零退出"));
                if (!string.IsNullOrWhiteSpace(result.Failure))
                    _output.AppendText("Failure: " + result.Failure + Environment.NewLine);
            }
            catch (Exception ex)
            {
                if (!_closed)
                {
                    _state.Text = "运行失败";
                    _state.Foreground = Brushes.Firebrick;
                    _result.Text = ex.Message;
                    _output.AppendText(ex.ToString() + Environment.NewLine);
                }
            }
            finally
            {
                _running = false;
                _scope.IsEnabled = true;
                if (_cancel != null) { _cancel.Dispose(); _cancel = null; }
                if (!_closed) FireChanged();
            }
        }
        private void CancelCurrent()
        {
            if (_running && _cancel != null)
            {
                _state.Text = "正在停止子进程及其工作进程…";
                _cancel.Cancel();
            }
        }
        private void ClearOutput()
        {
            if (_running) return;
            _output.Clear();
            _messageCount = 0;
            _result.Text = "输出已清空；原 ArchitectureSmoke 独立程序与其他测试结果不变。";
            FireChanged();
        }
        private void FireChanged()
        {
            if (!_closed && StateChanged != null) StateChanged();
        }
        public void OnWorkspaceClosed()
        {
            _closed = true;
            if (_cancel != null) _cancel.Cancel();
            _runner.OutputReceived -= OnProcessOutput;
            _runner.Dispose();
        }
    }
}
