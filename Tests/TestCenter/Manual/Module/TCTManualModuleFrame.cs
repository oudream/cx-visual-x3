using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Mechanism.Lifecycle.Modules;
using CxVisual.Professional.Simulation.Modules;
using CxVisual.Professional.Vision.Modules;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 使用真实 FCM/MHS 配置和 MPW 通用编辑器的模块配置测试入口。
    /// 此处创建的是可保存的 Module 配置实例/描述，不伪装为设备已连接或 MEM 已登记的工程资源。
    /// </summary>
    public sealed class TCTManualModuleFrame : UserControl, ITCTWorkspaceFrame
    {
        private readonly MHSConfigService _service;
        private readonly ComboBox _moduleKind = new ComboBox { Width = 155, Margin = new Thickness(6, 0, 16, 0) };
        private readonly TextBox _moduleId = new TextBox {
            Width = 190, Text = "Module01", Padding = new Thickness(6, 3, 6, 3),
            Margin = new Thickness(6, 0, 0, 0)
        };
        private readonly ContentControl _editorHost = new ContentControl();
        private readonly TextBlock _status = new TextBlock {
            Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 6)
        };
        private IMPWConfigDraftGuard _draftGuard;
        private IDisposable _activeEditor;
        private Func<string> _validate;
        private string _activeId;
        private MFMModuleKind? _activeKind;
        private bool _opening;

        public string WorkspaceId { get { return "manual.module"; } }
        public event Action StateChanged;
        public bool HasOpenedConfiguration { get { return _activeEditor != null; } }
        public string ActiveModuleId { get { return _activeId; } }
        public MFMModuleKind? ActiveModuleKind { get { return _activeKind; } }

        public System.Collections.Generic.IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                return new [] {
                    new TCTWorkspaceAction("module.open", "创建 / 打开模块配置", OpenSelected, () => !_opening),
                    new TCTWorkspaceAction("module.validate", "验证当前模块配置", ValidateCurrent,
                        () => HasOpenedConfiguration)
                };
            }
        }

        public TCTManualModuleFrame()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CxVisual", "TestCenter", "ModuleConfigurations");
            _service = new MHSConfigService(root);
            // 原服务的 RegisterAssembly 是真实 Schema 注册与冲突检测，非新增模块 Catalog。
            _service.RegisterAssembly(typeof(MFMModuleIdentityConfig).Assembly);
            _service.RegisterAssembly(typeof(MCNConnectorEditableConfig).Assembly);
            _service.RegisterAssembly(typeof(PVCComputeEditableConfig).Assembly);
            _service.RegisterAssembly(typeof(PSMSimulationEditableConfig).Assembly);
            _service.RegisterAssembly(typeof(MLCServiceEditableConfig).Assembly);
            _moduleKind.ItemsSource = Enum.GetValues(typeof(MFMModuleKind));
            _moduleKind.SelectedItem = MFMModuleKind.Connector;

            DockPanel rootPanel = new DockPanel { Margin = new Thickness(16) };
            StackPanel header = new StackPanel();
            header.Children.Add(new TextBlock {
                Text = "模块创建与配置 / 统一配置体系", FontSize = 20,
                Foreground = Brushes.DarkSlateBlue, FontWeight = FontWeights.SemiBold
            });
            header.Children.Add(new TextBlock {
                Text = "选模块类型、输入模块 Id，再从右侧操作中心创建/打开配置。参数表单来自 FCM Attribute + MHS + MPW，未复制第二套编辑器。",
                Foreground = Brushes.SlateGray, Margin = new Thickness(0, 7, 0, 7),
                TextWrapping = TextWrapping.Wrap
            });
            WrapPanel picker = new WrapPanel { Orientation = Orientation.Horizontal };
            picker.Children.Add(new TextBlock { Text = "模块类型", VerticalAlignment = VerticalAlignment.Center });
            picker.Children.Add(_moduleKind);
            picker.Children.Add(new TextBlock { Text = "模块 Id", VerticalAlignment = VerticalAlignment.Center });
            picker.Children.Add(_moduleId);
            header.Children.Add(picker);
            header.Children.Add(_status);
            header.Children.Add(new TextBlock {
                Text = "当前验证：配置 Schema、组件列表与约束、Saved/Effective/版本。创建配置 ≠ 启动设备、构建工程或通过自动测试。",
                Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 7)
            });
            DockPanel.SetDock(header, Dock.Top);
            rootPanel.Children.Add(header);
            _editorHost.Content = new TextBlock {
                Text = "右侧操作中心 → 创建 / 打开模块配置", Foreground = Brushes.Gray,
                Margin = new Thickness(14, 22, 14, 0)
            };
            rootPanel.Children.Add(_editorHost);
            Content = rootPanel;
            UpdateStatus("请选择模块类型和 Id，然后创建配置实例。");
            System.Windows.Automation.AutomationProperties.SetAutomationId(this, "TCT.Manual.Module");
        }

        private void UpdateStatus(string message)
        {
            _status.Text = message;
            Action state = StateChanged;
            if (state != null) state();
        }

        private static string RequireId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("模块 Id 不能为空");
            string value = id.Trim();
            if (value.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(value,
                @"\A[A-Za-z][A-Za-z0-9_.-]*\z"))
                throw new ArgumentException("模块 Id 仅允许 1～64 位英文字母开头及数字、点、横线、下划线");
            return value;
        }

        // 右侧操作入口是同步 Action；该方法自行捕获异步错误并向 Frame 状态报告。
        public async void OpenSelected()
        {
            if (_opening) return;
            _opening = true;
            try
            {
                if (_moduleKind.SelectedItem == null) throw new InvalidOperationException("请选择模块分类");
                MFMModuleKind kind = (MFMModuleKind)_moduleKind.SelectedItem;
                string id = RequireId(_moduleId.Text);
                if (_draftGuard != null && _draftGuard.HasUnsavedDraft)
                {
                    UpdateStatus("当前表单存在未保存草稿，请先保存或处理草稿，再切换模块。");
                    return;
                }
                if (_activeEditor != null && _activeId == id && _activeKind == kind)
                {
                    UpdateStatus("已打开当前模块配置；可以直接编辑。");
                    return;
                }
                MHSConfigSection<MFMModuleIdentityConfig> identity =
                    _service.ForScope("module", id).Open<MFMModuleIdentityConfig>();
                MFMModuleIdentityConfig saved = identity.GetLatestSaved();
                if (saved.Kind.HasValue && saved.Kind.Value != kind)
                {
                    UpdateStatus("模块 " + id + " 已登记为 " + saved.Kind.Value +
                        "；不允许在同一个身份下创建另一种模块。");
                    return;
                }
                if (!saved.Kind.HasValue)
                {
                    MHSConfigCommitResult result = await identity.UpdateAsync(
                        model => model.Kind = kind, "创建四类模块配置身份", CancellationToken.None);
                    if (!result.Success)
                    {
                        UpdateStatus("模块身份提交失败：" + (result.ErrorCode ?? "CONFIG_COMMIT_REJECTED"));
                        return;
                    }
                }
                switch (kind)
                {
                    case MFMModuleKind.Connector:
                        Attach(_service.ForScope("module", id).Open<MCNConnectorEditableConfig>(), id, kind,
                            model => "Channel " + model.Channels.Count + " / Protocol " + model.Protocols.Count +
                                " / Points " + model.Points.Count);
                        break;
                    case MFMModuleKind.Compute:
                        Attach(_service.ForScope("module", id).Open<PVCComputeEditableConfig>(), id, kind,
                            model => { model.ToOptions(); return "Compute 资源配置合法"; });
                        break;
                    case MFMModuleKind.Simulation:
                        Attach(_service.ForScope("module", id).Open<PSMSimulationEditableConfig>(), id, kind,
                            model => { model.ToDefinition(); return "Simulation 模型定义合法"; });
                        break;
                    case MFMModuleKind.Service:
                        Attach(_service.ForScope("module", id).Open<MLCServiceEditableConfig>(), id, kind,
                            model => { model.ToOptions(); return "Service 生命周期配置合法"; });
                        break;
                    default:
                        throw new InvalidOperationException("未知模块分类");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus("模块配置创建/打开失败：" + ex.GetType().Name + "，请检查模块 Id、配置类型和存储状态。");
            }
            finally
            {
                _opening = false;
                Action action = StateChanged;
                if (action != null) action();
            }
        }

        private void Attach<T>(MHSConfigSection<T> section, string id, MFMModuleKind kind,
            Func<T, string> extra) where T : MFMModuleEditableConfig, new()
        {
            MPWConfigEditorControl<T> next = new MPWConfigEditorControl<T>(section);
            IDisposable old = _activeEditor;
            _activeEditor = next;
            _draftGuard = next;
            _validate = () => {
                T draft = next.Draft;
                FCMConfigValidationReport report = FCMConfigValidationEngine.Validate(draft, "module:" + id);
                if (!report.IsValid)
                    return "配置存在 " + report.Issues.Count(x => x.Severity == FCMConfigIssueSeverity.Error) +
                        " 个问题：" + string.Join("；", report.Issues.Where(x => x.Severity == FCMConfigIssueSeverity.Error)
                          .Take(5).Select(x => x.Path + " " + x.Code));
                MFMModuleDescriptor descriptor = draft.Describe(id, kind);
                string extraResult = extra(draft);
                return descriptor.Kind + " / " + descriptor.Id + "：结构验证通过；" + extraResult +
                    "。尚未做设备连接和 MRK 运行验证。";
            };
            _activeId = id; _activeKind = kind;
            _editorHost.Content = next;
            if (old != null) old.Dispose();
            UpdateStatus("已创建/打开配置范围 module/" + id + "，类型 " + kind +
                "。通用表单负责校验、保存、生效与版本；并非已经创建运行实例。");
        }

        public void ValidateCurrent()
        {
            if (_validate == null) throw new InvalidOperationException("尚未打开模块配置");
            try { UpdateStatus(_validate()); }
            catch (Exception error) { UpdateStatus("模块结构校验失败：" + error.GetType().Name + "，请检查配置。"); }
        }

        public void OnWorkspaceClosed()
        {
            if (_activeEditor != null) _activeEditor.Dispose();
            _editorHost.Content = null;
            _activeEditor = null; _draftGuard = null; _validate = null;
            _activeId = null; _activeKind = null;
        }
    }
}
