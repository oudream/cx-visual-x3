using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Foundation.Data.Messages;
using CxVisual.Mechanism.Data.Messages;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// M36真实进程内消息手工测试面板。不是Broker，也不伪造硬件投递或正式TCT Run。
    /// 每个打开的工作区实例独占MessageCenter和订阅，关闭页面释放全部资源。
    /// </summary>
    public sealed class TCTManualMessageFrame : UserControl, ITCTWorkspaceFrame
    {
        private sealed class MessageRow
        {
            public string Time { get; set; }
            public string Kind { get; set; }
            public string Topic { get; set; }
            public string Content { get; set; }
            public string Result { get; set; }
        }

        private readonly MNMMessageCenter _center = new MNMMessageCenter();
        private readonly ObservableCollection<MessageRow> _rows = new ObservableCollection<MessageRow>();
        private readonly TextBox _topic = Field("/machine/1201/app/XRay/module/Camera/message/Captured");
        private readonly TextBox _filter = Field("/machine/1201/app/XRay/module/+/message/#");
        private readonly TextBox _replyTopic = Field("/machine/1201/app/XRay/module/Camera/message/Reply");
        private readonly TextBox _payload = Field("Image001");
        private readonly TextBox _source = Field("TestCenter");
        private readonly ComboBox _encoding = new ComboBox { MinWidth = 138, Height = 30, Margin = new Thickness(3) };
        private readonly ComboBox _dispatch = new ComboBox { MinWidth = 138, Height = 30, Margin = new Thickness(3) };
        private readonly CheckBox _retain = new CheckBox { Content = "Retain", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 0, 0, 0) };
        private readonly CheckBox _qos = new CheckBox { Content = "进程内QoS1", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 0, 0, 0) };
        private readonly CheckBox _localResponder = new CheckBox { Content = "启用本地测试应答（非设备ACK）", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SteelBlue, Margin = new Thickness(4, 6, 4, 6), FontSize = 15 };
        private readonly TextBlock _metrics = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SlateGray, Margin = new Thickness(4, 6, 4, 6), FontSize = 14 };
        private IDisposable _subscription;
        private IDisposable _responder;
        private bool _disposed;
        private bool _busy;
        public string WorkspaceId { get { return "manual.message"; } }
        public Guid ManualSessionId { get; private set; }
        public event Action StateChanged;
        public int ObservedCount { get { return _rows.Count; } }
        public string StatusSummary { get { return _status.Text; } }
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                return new [] {
                    new TCTWorkspaceAction("message.subscribe", "订阅 Topic Filter", Subscribe, () => !_disposed),
                    new TCTWorkspaceAction("message.unsubscribe", "取消订阅", Unsubscribe, () => !_disposed && _subscription != null),
                    new TCTWorkspaceAction("message.publish", "发布消息", () => RunCommand(PublishLocalAsync), () => !_disposed && !_busy),
                    new TCTWorkspaceAction("message.request", "请求 / 响应", () => RunCommand(RequestLocalAsync), () => !_disposed && !_busy),
                    new TCTWorkspaceAction("message.metrics", "刷新指标", RefreshMetrics, () => !_disposed),
                    new TCTWorkspaceAction("message.pause", "暂停分发", Pause, () => !_disposed && _center.State == MNMMessageState.Running),
                    new TCTWorkspaceAction("message.resume", "恢复分发", Resume, () => !_disposed && _center.State == MNMMessageState.Paused),
                    new TCTWorkspaceAction("message.clearlog", "清空观察记录", ClearLog, () => !_disposed)
                };
            }
        }

        private static TextBox Field(string text)
        {
            return new TextBox { Text = text, Height = 30, MinWidth = 130,
                VerticalContentAlignment = VerticalAlignment.Center, FontSize = 14,
                Margin = new Thickness(3) };
        }
        private static TextBlock Label(string text)
        {
            return new TextBlock { Text = text, Foreground = Brushes.SlateGray,
                Margin = new Thickness(4, 8, 4, 2), FontSize = 14 };
        }
        private static Button ActionButton(string caption, string automationId, Action handler)
        {
            Button button = new Button { Content = caption, Padding = new Thickness(13, 6, 13, 6),
                Margin = new Thickness(3), Background = Brushes.AliceBlue,
                BorderBrush = Brushes.LightSteelBlue, MinHeight = 34 };
            AutomationProperties.SetAutomationId(button,automationId);
            button.Click += (sender,args) => handler();
            return button;
        }
        private static StackPanel Horizontal()
        {
            return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        }

        public TCTManualMessageFrame()
        {
            ManualSessionId = Guid.NewGuid();
            _encoding.Items.Add("Text");
            _encoding.Items.Add("JSON文本（string）");
            _encoding.Items.Add("Binary Hex");
            _encoding.SelectedIndex = 0;
            _dispatch.Items.Add(MNMDispatchMode.Queued);
            _dispatch.Items.Add(MNMDispatchMode.Inline);
            _dispatch.Items.Add(MNMDispatchMode.ThreadPool);
            _dispatch.SelectedIndex = 0;
            _center.InterceptorError += ex => Append("DIAG", "", "", ex.Message);
            BuildView();
            _status.Text = "本地进程内 MessageCenter 已创建 · Session=" +
                ManualSessionId.ToString("N").Substring(0,8) + " · 未连接外部MQTT Broker";
            RefreshMetrics();
        }

        private void BuildView()
        {
            DockPanel root = new DockPanel { Background = Brushes.White, Margin = new Thickness(12) };
            StackPanel top = new StackPanel();
            DockPanel.SetDock(top,Dock.Top);
            top.Children.Add(new TextBlock { Text = "M36 · 消息中心 / Topic Publish · Subscribe",
                FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = Brushes.SteelBlue,
                Margin = new Thickness(4, 4, 4, 6) });
            top.Children.Add(new TextBlock { Text = "仅使用正式MNMMessageCenter；本页面消息只在当前TestCenter进程内流转。普通通知不等于设备命令或硬件ACK。",
                Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4,0,4,8) });

            top.Children.Add(Label("发布 Topic（必须是完整具体Topic，不允许通配符）"));
            top.Children.Add(_topic);
            top.Children.Add(Label("订阅过滤器（MQTT + 单层，# 尾部多层）"));
            top.Children.Add(_filter);
            top.Children.Add(Label("消息 Payload"));
            _payload.MinHeight = 44; _payload.Height = Double.NaN; _payload.AcceptsReturn = true;
            _payload.TextWrapping = TextWrapping.Wrap;
            top.Children.Add(_payload);
            StackPanel settings = Horizontal();
            settings.Children.Add(Label("形式"));
            settings.Children.Add(_encoding);
            settings.Children.Add(Label("分发"));
            settings.Children.Add(_dispatch);
            settings.Children.Add(_retain); settings.Children.Add(_qos);
            top.Children.Add(settings);
            StackPanel settings2=Horizontal();
            settings2.Children.Add(Label("来源"));
            _source.Width=150; settings2.Children.Add(_source);
            settings2.Children.Add(Label("回复 Topic"));
            _replyTopic.MinWidth=275; settings2.Children.Add(_replyTopic);
            top.Children.Add(settings2);
            top.Children.Add(_localResponder);
            top.Children.Add(new TextBlock {
                Text = "操作命令位于右侧「操作中心」：订阅、发布、请求响应、暂停与刷新指标。",
                Foreground = Brushes.SlateGray, FontSize = 13, Margin = new Thickness(4, 5, 4, 5) });
            top.Children.Add(_status);
            root.Children.Add(top);

            Grid body = new Grid { Margin = new Thickness(3,5,3,4) };
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1,GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(90) });
            DataGrid table = new DataGrid { ItemsSource = _rows, IsReadOnly=true,
                AutoGenerateColumns=true, CanUserAddRows=false, SelectionMode=DataGridSelectionMode.Single,
                GridLinesVisibility=DataGridGridLinesVisibility.Horizontal,
                Background=Brushes.White, FontSize=13 };
            AutomationProperties.SetAutomationId(table, "TCT.Message.History");
            Grid.SetRow(table,0);body.Children.Add(table);
            StackPanel diagnostics=new StackPanel { Margin=new Thickness(2,6,2,0) };
            diagnostics.Children.Add(Label("实际消息指标 · Published / Delivered / Queue / 订阅 / 失败 / P95"));
            diagnostics.Children.Add(_metrics);
            Grid.SetRow(diagnostics,1);body.Children.Add(diagnostics);
            root.Children.Add(body);
            Content=root;
        }

        private void Changed()
        {
            Action cb=StateChanged;
            if(cb!=null) cb();
        }
        private void Show(string message)
        {
            _status.Text=message;Changed();
        }
        private void Append(string kind,string topic,string payload,string result)
        {
            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => Append(kind,topic,payload,result)));
                return;
            }
            if(_disposed) return;
            _rows.Add(new MessageRow { Time=DateTime.Now.ToString("HH:mm:ss.fff"),
                Kind=kind, Topic=topic, Content=payload, Result=result });
            while(_rows.Count>250) _rows.RemoveAt(0);
        }

        public void Subscribe()
        {
            if(_disposed) throw new ObjectDisposedException("TCTManualMessageFrame");
            try
            {
                // 先注册新过滤器，再释放旧令牌；非法输入不会破坏已经生效的订阅。
                IDisposable subscription=_center.Subscribe<object>(_filter.Text,
                    ctx => Append("RX",ctx.Header.Topic,
                        DescribePayload(ctx.Message.Payload.Value),
                        "Id="+ctx.Header.MessageId.ToString("N").Substring(0,8)),
                    new MNMSubscriptionOptions { SubscriberId="TestCenter.Message.Observer",
                        ReplayRetained=true });
                IDisposable previous=_subscription;
                _subscription=subscription;
                if(previous!=null) previous.Dispose();
                Show("已订阅："+_filter.Text+"；支持Text/Binary/DTO观察，最多显示250条");
                RefreshMetrics();
            }
            catch(Exception e){Show("订阅失败："+e.Message);throw;}
        }
        public void Unsubscribe()
        {
            if(_subscription!=null){_subscription.Dispose();_subscription=null;}
            Show("当前订阅已注销（已开始的回调可能仍在完成）");
            RefreshMetrics();
        }
        private static string DescribePayload(object payload)
        {
            byte[] bytes=payload as byte[];
            return bytes==null ? Convert.ToString(payload) : BitConverter.ToString(bytes);
        }
        private object BuildPayload()
        {
            if(_encoding.SelectedIndex!=2) return _payload.Text;
            string hex=new string(_payload.Text.Where(c=>!char.IsWhiteSpace(c)&&c!='-').ToArray());
            if(hex.Length==0||hex.Length%2!=0)throw new FormatException("Hex Buffer必须非空且长度为偶数");
            byte[] bytes=new byte[hex.Length/2];
            for(int i=0;i<bytes.Length;i++)
                bytes[i]=byte.Parse(hex.Substring(i*2,2),System.Globalization.NumberStyles.HexNumber);
            return bytes;
        }
        public async Task<MNMPublishReceipt> PublishLocalAsync()
        {
            if(_disposed) throw new ObjectDisposedException("TCTManualMessageFrame");
            object value=BuildPayload();
            MNMPublishOptions options=new MNMPublishOptions {
                SourceId=_source.Text, Retain=_retain.IsChecked==true,
                QoS=_qos.IsChecked==true ? MNMMessageQoS.AtLeastOnceInProcess : MNMMessageQoS.AtMostOnce,
                DispatchMode=(MNMDispatchMode)_dispatch.SelectedItem };
            MNMPublishReceipt receipt=await _center.PublishAsync<object>(_topic.Text,value,options)
                .ConfigureAwait(true);
            Append("TX",_topic.Text,DescribePayload(value),receipt.Stage.ToString()+
                " / 匹配="+receipt.MatchedCount+" / 交付="+receipt.DeliveredCount+
                " / 失败="+receipt.FailedCount);
            Show("发布结果："+receipt.Stage+"；Accepted≠设备执行成功；"+(receipt.Error??"无错误"));
            RefreshMetrics();
            return receipt;
        }
        public async Task<MNMMessageContext<string>> RequestLocalAsync()
        {
            if(_disposed) throw new ObjectDisposedException("TCTManualMessageFrame");
            if(_localResponder.IsChecked==true && _responder==null)
                _responder=_center.SubscribeAsync<string>(_topic.Text,
                    ctx => ctx.ReplyAsync("LOCAL-TEST-ACK:"+ctx.Payload),
                    new MNMSubscriptionOptions{SubscriberId="TestCenter.Message.LocalResponder",
                        ReplayRetained=false});
            if(_localResponder.IsChecked!=true&&_responder!=null)
            { _responder.Dispose();_responder=null; }
            MNMMessageContext<string> response=await _center.RequestAsync<string,string>(
                _topic.Text,_payload.Text,_replyTopic.Text,TimeSpan.FromSeconds(2)).ConfigureAwait(true);
            Append("REPLY",response.Header.Topic,response.Payload,
                "Correlation="+response.Header.CorrelationId.ToString("N").Substring(0,8));
            Show("本地测试应答已关联；此结果不代表远程设备ACK");
            RefreshMetrics();
            return response;
        }
        private async void RunCommand(Func<Task> action)
        {
            if(_busy||_disposed) return;
            _busy=true;Changed();
            try{await action().ConfigureAwait(true);}
            catch(Exception error)
            {
                Show(error.GetType().Name+": "+error.Message);
                Append("ERR",_topic.Text,"",error.Message);
            }
            finally{_busy=false;Changed();}
        }
        public void RefreshMetrics()
        {
            if(_disposed) return;
            MNMMessageMetrics m=_center.Metrics();
            _metrics.Text="Published "+m.Published+"   Delivered "+m.Delivered+
                "   Queue "+m.QueueDepth+"   Subs "+m.SubscriptionCount+
                "   Failed "+m.Failed+"   Retain "+m.RetainedCount+
                "   P95 "+m.P95DeliveryMilliseconds.ToString("F2")+"ms";
        }
        private void Pause(){_center.Pause();Show("中心暂停：队列消息暂不消费");}
        private void Resume(){_center.Resume();Show("中心已恢复运行");}
        private void ClearLog(){_rows.Clear();Show("本地观察记录已清空；消息中心仍然运行");}
        public void OnWorkspaceClosed()
        {
            if(_disposed)return;
            if(_subscription!=null){_subscription.Dispose();_subscription=null;}
            if(_responder!=null){_responder.Dispose();_responder=null;}
            _center.Dispose();
            _disposed=true;
            // 手工会话关闭仅释放订阅与进程内消息，不能声明现场设备停止。
        }
    }
}
