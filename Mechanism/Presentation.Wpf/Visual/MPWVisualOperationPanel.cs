using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>Studio/TestCenter共享的图元操作面板。只发出编辑意图，不持有MEM或Undo。</summary>
    public sealed class MPWVisualOperationPanel : UserControl
    {
        private readonly StackPanel _root=new StackPanel();
        private readonly StackPanel _commands=new StackPanel();
        private readonly Dictionary<string,Button> _buttons=new Dictionary<string,Button>(StringComparer.Ordinal);
        public event Action<string> OperationRequested;
        private string _activeTool="Select";
        public string ActiveTool {get{return _activeTool;}}
        /// <summary>仅显示活跃工具，工具状态本身由SceneView管理。</summary>
        public void SetActiveTool(string tool)
        {
            _activeTool=tool??"Select";
            foreach(var pair in _buttons)
            {
                bool active=pair.Key==_activeTool;
                pair.Value.FontWeight=active?FontWeights.Bold:FontWeights.Normal;
                pair.Value.BorderBrush=active?Brushes.DodgerBlue:Brushes.LightSteelBlue;
                pair.Value.BorderThickness=new Thickness(active?2:1);
            }
        }
        public IReadOnlyCollection<string> Operations { get { return _buttons.Keys; } }
        public bool ShowEditingActions
        {
            get { return _commands.Visibility==Visibility.Visible; }
            set { _commands.Visibility=value?Visibility.Visible:Visibility.Collapsed; }
        }
        public MPWVisualOperationPanel()
        {
            AutomationProperties.SetAutomationId(this,"MPW.Visual.Operations");
            AddSection(_root,"工具",new [] {"Select|选择 / 框选"});
            AddSection(_root,"基础图元",new [] {
                "Line|直线","Rect|矩形","Circle|圆","Ellipse|椭圆",
                "Polyline|折线","Polygon|多边形","Arc|圆弧","Bezier|贝塞尔",
                "Path|路径","Text|文本" });
            AddSection(_root,"Widget 控件",new [] {
                "WidgetButton|按钮","core.widget.textbox|文本框",
                "core.widget.checkbox|复选框","core.widget.slider|滑块",
                "core.widget.progressbar|进度条","core.widget.combobox|下拉框" });
            AddSection(_commands,"编辑 / 布局 / 连线",new [] {
                "edit.undo|撤销","edit.redo|重做","edit.delete|删除",
                "edit.duplicate|复制一份",
                "edit.group|组合","edit.ungroup|解组",
                "layer.front|置于顶层","layer.back|置于底层",
                "align.left|左对齐","align.right|右对齐",
                "align.top|顶对齐","align.bottom|底对齐",
                "align.centerX|水平居中","align.centerY|垂直居中",
                "align.distributeGapX|水平等间距","align.distributeGapY|垂直等间距",
                "route.straight|直线连接","route.orthogonal|正交连接",
                "route.manual|人工路径" });
            _root.Children.Add(_commands);
            Content=_root;
            // 初始尚无选择和历史；禁止画面显示可点但无法执行的编辑命令。
            UpdateAvailability(0,false,false,false,true);
            SetActiveTool("Select");
        }
        private void AddSection(Panel parent,string heading,IEnumerable<string> values)
        {
            parent.Children.Add(new TextBlock { Text=heading,FontSize=13,
                FontWeight=FontWeights.SemiBold,Margin=new Thickness(2,9,0,5) });
            UniformGrid grid=new UniformGrid {Columns=2};
            foreach(string value in values)
            {
                int split=value.IndexOf('|');
                string id=value.Substring(0,split);
                Button button=new Button {Content=value.Substring(split+1),Tag=id,
                    Margin=new Thickness(2),MinHeight=28,ToolTip=heading+" · "+value.Substring(split+1)};
                AutomationProperties.SetAutomationId(button,"MPW.Visual."+id);
                // 此处仅转发用户意图，执行和权限由宿主统一MVI/MEM编辑入口负责。
                button.Click+=(sender,args)=>{var requested=OperationRequested;if(requested!=null)requested(id);};
                _buttons.Add(id,button);grid.Children.Add(button);
            }
            parent.Children.Add(grid);
        }
        /// <summary>
        /// 仅计算公共按钮的呈现可用性，实际权限/作用域仍由宿主MVI/MEM最终校验。
        /// 多选要求两个真实对象；视觉连接模式只对选择的一条MPC连接开放。
        /// 不在控件中保存选区或维护第二套命令状态。
        /// </summary>
        public void UpdateAvailability(int selectionCount,bool selectedVisualConnection,
            bool canUndo,bool canRedo,bool canEdit,bool selectedIsGroup=false)
        {
            SetOperationEnabled("edit.undo",canUndo&&canEdit);
            SetOperationEnabled("edit.redo",canRedo&&canEdit);
            SetOperationEnabled("edit.delete",canEdit&&selectionCount==1);
            SetOperationEnabled("edit.duplicate",canEdit&&selectionCount>=1&&!selectedVisualConnection);
            SetOperationEnabled("edit.group",canEdit&&selectionCount>=2&&!selectedVisualConnection);
            SetOperationEnabled("edit.ungroup",canEdit&&selectionCount==1&&selectedIsGroup);
            SetOperationEnabled("layer.front",canEdit&&selectionCount==1&&!selectedVisualConnection);
            SetOperationEnabled("layer.back",canEdit&&selectionCount==1&&!selectedVisualConnection);
            foreach(string id in new[]{"align.left","align.right","align.top","align.bottom",
                "align.centerX","align.centerY"})
                SetOperationEnabled(id,canEdit&&selectionCount>=2&&!selectedVisualConnection);
            foreach(string id in new[]{"align.distributeGapX","align.distributeGapY"})
                SetOperationEnabled(id,canEdit&&selectionCount>=3&&!selectedVisualConnection);
            foreach(string id in new[]{"route.straight","route.orthogonal","route.manual"})
                SetOperationEnabled(id,canEdit&&selectionCount==1&&selectedVisualConnection);
            SetOperationEnabled("Select",true);
            foreach(string id in new[]{"Line","Rect","Circle","Ellipse","Polyline","Polygon",
                "Arc","Bezier","Path","Text","WidgetButton","core.widget.textbox",
                "core.widget.checkbox","core.widget.slider","core.widget.progressbar",
                "core.widget.combobox"})
                SetOperationEnabled(id,canEdit);
        }
        public void SetOperationEnabled(string id,bool enabled)
        {
            Button button;
            if(_buttons.TryGetValue(id,out button))button.IsEnabled=enabled;
        }
    }
}