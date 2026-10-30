using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Messages;
using CxVisual.Mechanism.Data.Messages;

namespace CxVisual.Mechanism.Presentation.Wpf.Interaction
{
    /// <summary>
    /// 可共用的MNM消息观察/人工操作界面。MNM Owner由宿主注入，
    /// 不创建进程全局MessageCenter，不将订阅或运行数据写入MEM。
    /// </summary>
    public sealed class MPWMessageCenterControl : UserControl, IDisposable
    {
        private sealed class EventRow
        {
            public string Time { get; set; }
            public string Direction { get; set; }
            public string Topic { get; set; }
            public string Payload { get; set; }
            public string Result { get; set; }
        }
        private readonly ObservableCollection<EventRow> _events = new ObservableCollection<EventRow>();
        private readonly TextBox _topic = new TextBox { Text="/machine/1201/app/XRay/module/Camera/message/Captured", MinWidth=330, Margin=new Thickness(4) };
        private readonly TextBox _filter = new TextBox { Text="/machine/1201/app/XRay/module/+/message/#", MinWidth=330, Margin=new Thickness(4) };
        private readonly TextBox _payload = new TextBox { Text="Image001", MinWidth=200, Margin=new Thickness(4) };
        private readonly TextBlock _status = new TextBlock { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(7) };
        private readonly TextBlock _metrics = new TextBlock { TextWrapping=TextWrapping.Wrap, Margin=new Thickness(7) };
        private readonly Button _publish = new Button { Content="发布文本消息", Padding=new Thickness(10,5), Margin=new Thickness(3) };
        private readonly CheckBox _retain = new CheckBox { Content="Retain", Margin=new Thickness(7,5,7,5) };
        private MNMMessageCenter _owner;
        private IDisposable _subscription;
        private bool _publishAllowed;
        private bool _disposed;
        private int _generation;
        public int ObservedCount { get { return _events.Count; } }
        public bool Connected { get { return _owner!=null; } }
        public string StatusSummary { get { return _status.Text; } }

        public MPWMessageCenterControl()
        {
            AutomationProperties.SetAutomationId(this,"MPW.MessageCenter");
            AutomationProperties.SetAutomationId(_topic,"MPW.MessageCenter.Topic");
            AutomationProperties.SetAutomationId(_filter,"MPW.MessageCenter.Filter");
            AutomationProperties.SetAutomationId(_publish,"MPW.MessageCenter.Publish");
            StackPanel header=new StackPanel { Margin=new Thickness(7) };
            header.Children.Add(new TextBlock { Text="消息中心 / MNM", FontSize=18, FontWeight=FontWeights.SemiBold });
            header.Children.Add(new TextBlock { Text="绑定宿主真实MessageCenter；订阅/发布不等于设备命令或现场ACK",
                TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,4,0,7) });
            WrapPanel filterLine=new WrapPanel();
            filterLine.Children.Add(new TextBlock { Text="订阅 Filter", VerticalAlignment=VerticalAlignment.Center });
            filterLine.Children.Add(_filter);
            filterLine.Children.Add(Button("订阅",()=>Subscribe()));
            filterLine.Children.Add(Button("取消订阅",Unsubscribe));
            header.Children.Add(filterLine);
            WrapPanel publishLine=new WrapPanel();
            publishLine.Children.Add(new TextBlock { Text="Topic", VerticalAlignment=VerticalAlignment.Center });
            publishLine.Children.Add(_topic);
            publishLine.Children.Add(new TextBlock { Text="Text", VerticalAlignment=VerticalAlignment.Center });
            publishLine.Children.Add(_payload);
            publishLine.Children.Add(_retain);
            _publish.Click+=async(s,e)=>{try{await PublishTextAsync();}catch(Exception ex){_status.Text="发布失败："+ex.Message;}};
            publishLine.Children.Add(_publish);
            publishLine.Children.Add(Button("刷新指标",RefreshMetrics));
            header.Children.Add(publishLine);
            DockPanel root=new DockPanel { Margin=new Thickness(7) };
            DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
            StackPanel footer=new StackPanel();
            footer.Children.Add(_metrics); footer.Children.Add(_status);
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            DataGrid table=new DataGrid { ItemsSource=_events, AutoGenerateColumns=true,
                CanUserAddRows=false,IsReadOnly=true, MinHeight=110 };
            AutomationProperties.SetAutomationId(table,"MPW.MessageCenter.Events");
            root.Children.Add(table);Content=root;
            RefreshMetrics();
        }
        private static Button Button(string label,Action action)
        {
            Button button=new Button {Content=label,Margin=new Thickness(3),Padding=new Thickness(10,5)};
            button.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(ex.Message,label);}};
            return button;
        }
        /// <summary>默认只读；发布权限是界面额外限制，实际授权仍由MNM宿主决定。</summary>
        public void Bind(MNMMessageCenter owner,bool allowPublish=false)
        {
            if(_disposed)throw new ObjectDisposedException("MPWMessageCenterControl");
            Unsubscribe();
            _generation++;
            _owner=owner;
            _publishAllowed=owner!=null&&allowPublish;
            _publish.IsEnabled=_publishAllowed;
            _events.Clear();
            _status.Text=owner==null?"未绑定消息宿主，所有操作不可用":
                "已绑定真实MNM；默认不替调用者建立全局总线，当前发布"+(_publishAllowed?"已允许":"只读禁用");
            RefreshMetrics();
        }
        public void Subscribe()
        {
            if(_disposed||_owner==null)throw new InvalidOperationException("当前没有已连接的消息中心");
            MNMMessageCenter current=_owner;
            int generation=_generation;
            IDisposable next=current.Subscribe<object>(_filter.Text,ctx=>{
                string topic=ctx.Header.Topic;
                string payload=ctx.Message.Payload.Value is byte[]
                    ? BitConverter.ToString((byte[])ctx.Message.Payload.Value)
                    : Convert.ToString(ctx.Message.Payload.Value);
                Dispatcher.BeginInvoke(new Action(()=>{
                    if(_disposed||generation!=_generation||!ReferenceEquals(_owner,current))return;
                    Append("RX",topic,payload,"消息Id="+ctx.Header.MessageId.ToString("N").Substring(0,8));
                }));
            },new MNMSubscriptionOptions {SubscriberId="MPW.MessageCenter."+GetHashCode(),ReplayRetained=true});
            IDisposable previous=_subscription;
            _subscription=next;
            if(previous!=null)previous.Dispose();
            _status.Text="订阅生效："+_filter.Text;
            RefreshMetrics();
        }
        public void Unsubscribe()
        {
            IDisposable prior=_subscription;_subscription=null;
            if(prior!=null)prior.Dispose();
        }
        public async System.Threading.Tasks.Task<MNMPublishReceipt> PublishTextAsync()
        {
            if(_disposed||_owner==null||!_publishAllowed)
                throw new UnauthorizedAccessException("未绑定可发布的消息宿主");
            MNMMessageCenter current=_owner;
            int generation=_generation;
            MNMPublishReceipt receipt=await current.PublishAsync<string>(_topic.Text,_payload.Text,
                new MNMPublishOptions {SourceId="MPW.MessageCenter",
                    Retain=_retain.IsChecked==true,DispatchMode=MNMDispatchMode.Queued});
            if(!_disposed&&generation==_generation&&ReferenceEquals(_owner,current))
            {
                Append("TX",_topic.Text,_payload.Text,
                    receipt.Stage+" / Delivered="+receipt.DeliveredCount+" / Failed="+receipt.FailedCount);
                _status.Text="消息发布结果："+receipt.Stage+"；消息ACK不是设备执行确认";
                RefreshMetrics();
            }
            return receipt;
        }
        private void Append(string direction,string topic,string payload,string result)
        {
            _events.Add(new EventRow {Time=DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction=direction,Topic=topic,Payload=payload,Result=result});
            while(_events.Count>250)_events.RemoveAt(0);
        }
        public void RefreshMetrics()
        {
            if(_owner==null){_metrics.Text="无运行指标（未连接）";return;}
            MNMMessageMetrics m=_owner.Metrics();
            _metrics.Text="Published="+m.Published+" Delivered="+m.Delivered+
                " Failed="+m.Failed+" Rejected="+m.Rejected+
                " Queue="+m.QueueDepth+" Subs="+m.SubscriptionCount+
                " P95="+m.P95DeliveryMilliseconds.ToString("F2")+" ms";
        }
        public void Dispose()
        {
            if(_disposed)return;
            Unsubscribe();_generation++;_owner=null;_disposed=true;
            _events.Clear();
        }
    }
}
