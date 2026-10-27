using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume
{
    /// <summary>
    /// 可嵌入的WPF四视图布局，继承成熟FourViewLayoutControl的2x2/单窗/恢复行为。
    /// 只拥有视图槽布局，不拥有FVO数据、MVI三刀状态、相机或GPU资源。
    /// 宿主传入同一套二维与三维控件，既可嵌入Studio也可独立运行。
    /// </summary>
    public sealed class MPWVolumeFourViewLayoutControl : Grid
    {
        private sealed class SlotEntry
        {
            public Grid Host;
            public int Row;
            public int Column;
        }

        private readonly List<SlotEntry> _slots = new List<SlotEntry>();
        private Grid _maximized;

        public MPWVolumeFourViewLayoutControl()
        {
            Background = new SolidColorBrush(Color.FromRgb(11,17,25));
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions.Add(new RowDefinition());
            ColumnDefinitions.Add(new ColumnDefinition());
            ColumnDefinitions.Add(new ColumnDefinition());
            AutomationProperties.SetAutomationId(this,"MPW.Volume.FourView");
        }

        public int ViewCount { get { return _slots.Count; } }
        public bool IsSingleView { get { return _maximized != null; } }
        public Grid MaximizedSlot { get { return _maximized; } }

        /// <summary>
        /// 注册一个已有视图实例。禁止重复占位或重复父级，不能创建第二份Viewer。
        /// 返回的槽标题行仍允许宿主扩展导航按钮与专业命令。
        /// </summary>
        public Grid AddView(UIElement content,string title,int row,int column)
        {
            if(content==null)throw new ArgumentNullException("content");
            if(String.IsNullOrWhiteSpace(title))
                throw new ArgumentException("视图标题不能为空。","title");
            if(row<0||row>1)throw new ArgumentOutOfRangeException("row");
            if(column<0||column>1)throw new ArgumentOutOfRangeException("column");
            foreach(SlotEntry existing in _slots)
                if(existing.Row==row&&existing.Column==column)
                    throw new InvalidOperationException("四视图位置已被占用。");
            if(_slots.Count>=4)
                throw new InvalidOperationException("四视图布局最多允许四个视图。");
            if(IsSingleView)
                throw new InvalidOperationException("请先恢复2x2布局后再添加视图。");

            Grid host = new Grid {
                Margin=new Thickness(2),
                Background=new SolidColorBrush(Color.FromRgb(8,14,22))
            };
            host.RowDefinitions.Add(new RowDefinition { Height=new GridLength(24) });
            host.RowDefinitions.Add(new RowDefinition());
            Border header = new Border {
                Background=new SolidColorBrush(Color.FromRgb(245,248,252)),
                Padding=new Thickness(6,2,4,2),
                Child=new TextBlock {
                    Text=title,Foreground=new SolidColorBrush(Color.FromRgb(36,65,105)),
                    FontSize=12,FontWeight=FontWeights.SemiBold
                }
            };
            host.Children.Add(header);
            Grid.SetRow(content,1);
            host.Children.Add(content);
            Grid.SetRow(host,row);
            Grid.SetColumn(host,column);
            AutomationProperties.SetAutomationId(host,
                "MPW.Volume.ViewSlot."+row+column);
            Children.Add(host);
            _slots.Add(new SlotEntry { Host=host,Row=row,Column=column });
            return host;
        }

        public Grid GetSlot(int index)
        {
            if(index<0||index>=_slots.Count)
                throw new ArgumentOutOfRangeException("index");
            return _slots[index].Host;
        }

        /// <summary>同一个槽再次切换时恢复2x2；原控件实例和原位置保持不变。</summary>
        public void ToggleSlot(Grid slot)
        {
            if(slot==null)throw new ArgumentNullException("slot");
            if(!_slots.Exists(x=>Object.ReferenceEquals(x.Host,slot)))
                throw new ArgumentException("视图槽不属于当前布局。","slot");
            if(Object.ReferenceEquals(_maximized,slot))
            {
                RestoreAllViews();
                return;
            }
            RestoreAllViews();
            _maximized=slot;
            foreach(SlotEntry item in _slots)
                if(!Object.ReferenceEquals(item.Host,slot))
                    item.Host.Visibility=Visibility.Collapsed;
            Grid.SetRow(slot,0);
            Grid.SetColumn(slot,0);
            Grid.SetRowSpan(slot,2);
            Grid.SetColumnSpan(slot,2);
            slot.Visibility=Visibility.Visible;
        }

        public void RestoreAllViews()
        {
            if(_maximized==null)return;
            foreach(SlotEntry item in _slots)
            {
                Grid.SetRow(item.Host,item.Row);
                Grid.SetColumn(item.Host,item.Column);
                Grid.SetRowSpan(item.Host,1);
                Grid.SetColumnSpan(item.Host,1);
                item.Host.Visibility=Visibility.Visible;
            }
            _maximized=null;
        }
    }
}
