using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CxECTImageWinForms.Controls
{
    /// <summary>
    /// 轻量命令面板。
    /// 固定按钮可以直接由宿主 Designer 放入本面板，因此 ImageViewerControl 设计器可真正所见即所得。
    /// 同时保留 IEnumerable 构造函数，兼容其它位置按配置动态创建按钮。
    /// </summary>
    public sealed partial class ToolActionPanel : FlowLayoutPanel
    {
        private readonly Dictionary<string, Button> _modeButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);

        private readonly HashSet<Button> _boundButtons = new HashSet<Button>();

        /// <summary>
        /// WinForms Designer 必须使用无参数构造函数。
        /// </summary>
        public ToolActionPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 兼容原有运行时动态创建方式。
        /// </summary>
        public ToolActionPanel(IEnumerable<ToolActionDefinition> actions)
            : this()
        {
            SetActions(actions);
        }

        public event EventHandler<ToolActionEventArgs> ActionRequested;

        /// <summary>
        /// 将 Designer 中直接放入本容器的 Button 绑定为工具按钮。
        /// Button.Tag 使用 "Command" 或 "ExclusiveMode"。
        /// </summary>
        public void BindDesignerButtons()
        {
            BindButtonsRecursive(this);
        }

        /// <summary>
        /// 兼容动态动作集合。调用后会清空 Designer 子按钮并重新创建。
        /// ImageViewerControl 本身不使用此方法，它的按钮全部在 Designer 中静态声明。
        /// </summary>
        public void SetActions(IEnumerable<ToolActionDefinition> actions)
        {
            SuspendLayout();
            try
            {
                Controls.Clear();
                _modeButtons.Clear();
                _boundButtons.Clear();

                if (actions == null)
                {
                    return;
                }

                foreach (var action in actions)
                {
                    if (action == null)
                    {
                        continue;
                    }

                    var button = CreateRuntimeButton(action.Action, action.Kind);
                    Controls.Add(button);
                    BindActionButton(button, action.Action, action.Kind);
                }
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        /// <summary>
        /// 宿主在模式变化时广播调用，使跨分页只保留一个高亮的模式按钮。
        /// </summary>
        public void SetSelectedAction(string action)
        {
            foreach (var item in _modeButtons)
            {
                var selected = string.Equals(item.Key, action, StringComparison.Ordinal);
                var button = item.Value;
                button.BackColor = selected ? ImageViewerPanelStyle.SelectedBackColor : Color.White;
                button.ForeColor = selected ? ImageViewerPanelStyle.InfoBlue : ImageViewerPanelStyle.TextColor;
                button.FlatAppearance.BorderColor = selected
                    ? Color.FromArgb(112, 170, 235)
                    : Color.FromArgb(214, 222, 232);
            }
        }

        private void BindButtonsRecursive(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                var button = control as Button;
                if (button != null)
                {
                    var kind = ParseKind(button.Tag);
                    BindActionButton(button, button.Text, kind);
                }

                if (control.HasChildren)
                {
                    BindButtonsRecursive(control);
                }
            }
        }

        private void BindActionButton(Button button, string action, ToolActionKind kind)
        {
            if (button == null || _boundButtons.Contains(button))
            {
                return;
            }

            _boundButtons.Add(button);

            if (kind == ToolActionKind.ExclusiveMode && !_modeButtons.ContainsKey(action))
            {
                _modeButtons.Add(action, button);
            }

            button.Click += delegate
            {
                if (kind == ToolActionKind.ExclusiveMode)
                {
                    SetSelectedAction(action);
                }

                RaiseAction(new ToolActionDefinition(action, kind));
            };
        }

        private static ToolActionKind ParseKind(object tag)
        {
            if (tag is ToolActionKind)
            {
                return (ToolActionKind)tag;
            }

            var text = tag == null ? string.Empty : Convert.ToString(tag);
            return string.Equals(text, "ExclusiveMode", StringComparison.OrdinalIgnoreCase)
                ? ToolActionKind.ExclusiveMode
                : ToolActionKind.Command;
        }

        private static Button CreateRuntimeButton(string text, ToolActionKind kind)
        {
            var button = new Button
            {
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                ForeColor = ImageViewerPanelStyle.TextColor,
                Height = 32,
                Margin = new Padding(3),
                Size = new Size(164, 32),
                Tag = kind.ToString(),
                Text = text,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderColor = Color.FromArgb(214, 222, 232);
            button.FlatAppearance.MouseOverBackColor = ImageViewerPanelStyle.SelectedBackColor;
            return button;
        }

        private void RaiseAction(ToolActionDefinition action)
        {
            var handler = ActionRequested;
            if (handler != null)
            {
                handler(this, new ToolActionEventArgs(action.Action, action.Kind));
            }
        }
    }

    /// <summary>
    /// 命令仅执行一次；互斥模式代表图窗当前的唯一输入工具。
    /// </summary>
    public enum ToolActionKind
    {
        Command,
        ExclusiveMode
    }

    /// <summary>
    /// 面板只描述 UI 行为，不依赖 HALCON 或具体业务模式，供各工具页复用。
    /// </summary>
    public sealed class ToolActionDefinition
    {
        public ToolActionDefinition(string action, ToolActionKind kind)
        {
            Action = action;
            Kind = kind;
        }

        public string Action { get; private set; }

        public ToolActionKind Kind { get; private set; }
    }

    public sealed class ToolActionEventArgs : EventArgs
    {
        public ToolActionEventArgs(string action, ToolActionKind kind)
        {
            Action = action;
            Kind = kind;
        }

        public string Action { get; private set; }

        public ToolActionKind Kind { get; private set; }
    }

    /// <summary>
    /// 工具面板统一视觉令牌。
    /// 固定按钮布局由 ImageViewerControl.Designer.cs 维护，这里只服务按钮状态。
    /// </summary>
    internal static class ImageViewerPanelStyle
    {
        internal static readonly Color CardBackColor = Color.FromArgb(249, 250, 252);
        internal static readonly Color SelectedBackColor = Color.FromArgb(234, 243, 255);
        internal static readonly Color InfoBlue = Color.FromArgb(45, 127, 230);
        internal static readonly Color TextColor = Color.FromArgb(42, 50, 64);
    }
}
