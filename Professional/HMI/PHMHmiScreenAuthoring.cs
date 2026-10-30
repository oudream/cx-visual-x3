using System;
using CxVisual.Mechanism.Core;

namespace CxVisual.Professional.HMI
{
    /// <summary>
    /// HMI专业组件按MEM原对象编写；使用唯一MTC事务/Undo/Serializer。
    /// 不创建平行Screen树，也不创建第二套Widget类或运行变量。
    /// </summary>
    public static class PHMHmiScreenAuthoring
    {
        private static MEMPage Page(MEMEngineeringSession project, Guid pageId)
        {
            if (project == null) throw new ArgumentNullException("project");
            MEMPage page = project.Find(pageId) as MEMPage;
            if (page == null || page.RootContent == null)
                throw new InvalidOperationException("hmi.screen.page_not_in_project");
            return page;
        }
        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 512)
                throw new ArgumentException("hmi.authoring.invalid_identity", name);
            return value.Trim();
        }
        private static void Position(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y) ||
                double.IsInfinity(x) || double.IsInfinity(y) ||
                Math.Abs(x) > 10000000 || Math.Abs(y) > 10000000)
                throw new ArgumentOutOfRangeException("hmi.authoring.position");
        }

        /// <summary>动态Numeric/Text使用MRCTextElement而非自定义WPF标签。</summary>
        public static Guid AddNumeric(MEMEngineeringSession project, Guid pageId,
            string pointName, string title, double x, double y)
        {
            MEMPage page = Page(project, pageId);
            string point = Required(pointName, "pointName");
            string name = Required(title, "title");
            Position(x, y);
            return project.Edit("HMI添加数值显示", e =>
            {
                MRCTextElement text = e.AddText(page.RootContent, name, "—", x, y, 175, 32);
                e.SetBinding(text, "text", MPMBinding.Variable(point));
                return text.Id;
            }).RequireValue();
        }

        /// <summary>布尔点运行时映射成灰/绿状态灯；质量非Good则恢复灰色，不显示假绿。</summary>
        public static Guid AddStatusLamp(MEMEngineeringSession project, Guid pageId,
            string booleanPoint, string title, double x, double y)
        {
            MEMPage page = Page(project, pageId);
            string point = Required(booleanPoint, "booleanPoint");
            string name = Required(title, "title");
            Position(x, y);
            return project.Edit("HMI添加状态灯", e =>
            {
                MRCRectangle lamp = e.AddRectangle(page.RootContent,
                    name + "状态", x, y, 32, 32);
                e.SetVisualStyle(lamp, MRCVisualStyle.Solid(
                    MRCColor.Parse("#FF7C8796")));
                e.SetVisualBehavior(lamp, new MRCVisualBehaviorDefinition(
                    states: new[] { new MRCVisualStateDefinition(point,
                        MRCVisualStyle.Solid(MRCColor.Parse("#FF21AC68"))) }));
                e.AddText(page.RootContent, name + "标签", name, x + 40, y + 3, 130, 29);
                return lamp.Id;
            }).RequireValue();
        }

        /// <summary>同一MVI/MPW的ProgressBar运行值绑定，范围由已有Widget Settings解释。</summary>
        public static Guid AddProgress(MEMEngineeringSession project, Guid pageId,
            string numericPoint, string title, double x, double y,
            double minimum, double maximum)
        {
            MEMPage page = Page(project, pageId);
            string point = Required(numericPoint, "numericPoint");
            string name = Required(title, "title");
            Position(x, y);
            if (double.IsNaN(minimum) || double.IsNaN(maximum) ||
                double.IsInfinity(minimum) || double.IsInfinity(maximum) ||
                maximum <= minimum)
                throw new ArgumentOutOfRangeException("maximum", "hmi.progress.invalid_range");
            return project.Edit("HMI添加进度条", e =>
            {
                MRCWidgetElement bar = (MRCWidgetElement)e.Add(page.RootContent,
                    new MRCWidgetElement(Guid.NewGuid(), name,
                        "core.widget.progressbar", x, y, 170, 26));
                e.SetBinding(bar, "value", MPMBinding.Variable(point));
                e.SetBinding(bar, "minimum", MPMBinding.Literal(MPMValue.Number(minimum)));
                e.SetBinding(bar, "maximum", MPMBinding.Literal(MPMValue.Number(maximum)));
                return bar.Id;
            }).RequireValue();
        }

        /// <summary>无命令时仅生成可编辑按钮，不生成隐含PLC写入。</summary>
        public static Guid AddButton(MEMEngineeringSession project, Guid pageId,
            string title, double x, double y)
        {
            MEMPage page = Page(project, pageId);
            string name = Required(title, "title");
            Position(x, y);
            return project.Edit("HMI添加待配置控制按钮", e =>
            {
                MRCWidgetElement button = (MRCWidgetElement)e.Add(page.RootContent,
                    new MRCWidgetElement(Guid.NewGuid(), name,
                        "core.widget.button", x, y, 130, 38));
                e.SetBinding(button, "content",
                    MPMBinding.Literal(MPMValue.String(name)));
                return button.Id;
            }).RequireValue();
        }

        /// <summary>
        /// 仅持久化外部正式命令Id；选择器/Host应核实注册Schema，
        /// 此API不建立命令、授权或PLC驱动，也不直接执行。
        /// </summary>
        public static void AssignSavedAction(MEMEngineeringSession project,
            Guid buttonId, string actionId, string parameterBindingId = "")
        {
            if (project == null) throw new ArgumentNullException("project");
            MRCWidgetElement button = project.Find(buttonId) as MRCWidgetElement;
            if (button == null || button.TypeId != "core.widget.button")
                throw new InvalidOperationException("hmi.action.not_a_button");
            string action = Required(actionId, "actionId");
            project.Edit("HMI配置已登记命令动作", e =>
            {
                MRCVisualBehaviorDefinition previous = MRCVisualAppearance.BehaviorOf(button);
                // 只替换Click定义，保留此Widget其它事件、动画及状态。
                var actions = new System.Collections.Generic.List<MRCVisualEventAction>();
                foreach (MRCVisualEventAction existing in previous.EventActions)
                    if (existing.EventId != "Click") actions.Add(existing);
                actions.Add(new MRCVisualEventAction("Click", action, parameterBindingId));
                e.SetVisualBehavior(button, new MRCVisualBehaviorDefinition(
                    previous.States, previous.Animations, actions));
            }).RequireValue();
        }
    }
}
