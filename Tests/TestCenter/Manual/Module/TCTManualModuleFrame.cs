using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly MPWModuleCenterControl _center=new MPWModuleCenterControl();
        private ContentControl _editorHost {get{return _center.EditorHost;}}
        private TabControl _pages {get{return _center.Pages;}}
        private Action<string[], string[]> _project;
        private Func<int> _visibleSchemaEditors;
        private bool _buildingPages;

        public string SelectedModulePage
        {
            get {
                MPWModuleCenterPage page=(_pages.SelectedItem as TabItem)?.Tag as MPWModuleCenterPage;
                return page==null?"":page.Id;
            }
        }
        public int VisibleSchemaEditorCount
        { get {return _visibleSchemaEditors==null?0:_visibleSchemaEditors();} }
        public void SelectModulePage(string pageId)
        {
            TabItem tab=_pages.Items.OfType<TabItem>().FirstOrDefault(item=>
                ((MPWModuleCenterPage)item.Tag).Id==pageId);
            if(tab==null)throw new ArgumentException("当前模块不支持该分页："+pageId);
            _pages.SelectedItem=tab;
        }
        private void RebuildPages(MFMModuleKind kind)
        {
            Type modelType;
            switch(kind)
            {
                case MFMModuleKind.Connector: modelType=typeof(MCNConnectorConfiguration);break;
                case MFMModuleKind.Driver: modelType=typeof(MFMDriverModuleConfiguration);break;
                case MFMModuleKind.Compute: modelType=typeof(PVCComputeModuleConfiguration);break;
                case MFMModuleKind.Simulation: modelType=typeof(PSMSimulationModuleConfiguration);break;
                case MFMModuleKind.Service: modelType=typeof(MLCServiceModuleConfiguration);break;
                default:throw new InvalidOperationException("未知模块类型");
            }
            _buildingPages=true;
            try
            {
                _pages.Items.Clear();
                foreach(MPWModuleCenterPage page in MPWModuleCenterPages.Create(kind,modelType))
                {
                    TabItem tab=new TabItem {Header=page.Label,Tag=page};
                    tab.SetResourceReference(FrameworkElement.StyleProperty,"MPWDocumentTabStyle");
                    System.Windows.Automation.AutomationProperties.SetAutomationId(tab,
                        "TCT.Module.Page."+page.Id);
                    _pages.Items.Add(tab);
                }
                _pages.SelectedIndex=0;
                _center.SetSelectedKind(kind);
            }
            finally{_buildingPages=false;}
            ProjectSelectedPage();
        }
        private void ProjectSelectedPage()
        {
            if(_buildingPages||_project==null)return;
            MPWModuleCenterPage page=(_pages.SelectedItem as TabItem)?.Tag as MPWModuleCenterPage;
            if(page!=null)_project(page.Fields,page.Categories);
        }

        private readonly TextBlock _status = new TextBlock {
            Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 3), MaxHeight = 24, TextTrimming = TextTrimming.CharacterEllipsis
        };
        private IMPWConfigDraftGuard _draftGuard;
        private IDisposable _activeEditor;
        private Func<string> _validate;
        private Func<CancellationToken, Task<MHSConfigCommitResult>> _saveDraft;
        private Func<FCMConfigApplyPolicy, MHSConfigApplyResult> _applySaved;
        private Func<string> _versions;
        private Action _refreshDeclaredCapabilities;
        private bool _busy;
        private string _activeId;
        private MFMModuleKind? _activeKind;
        private bool _opening;

        public string WorkspaceId { get { return "manual.module"; } }
        public event Action StateChanged;
        public bool HasOpenedConfiguration { get { return _activeEditor != null; } }
        public bool HasUnsavedDraft { get { return _draftGuard != null && _draftGuard.HasUnsavedDraft; } }
        public string ActiveModuleId { get { return _activeId; } }
        public MFMModuleKind? ActiveModuleKind { get { return _activeKind; } }
        public bool IsBusy { get { return _opening || _busy; } }
        public string SavedAndEffectiveVersions { get { return _versions == null ? "" : _versions(); } }
        // 只读当前正式MHS编辑草稿克隆，用于统一测试平台验证真实值与保存重开。
        public MCNConnectorConfiguration ConnectorDraftSnapshot()
        {
            MPWConfigEditorControl<MCNConnectorConfiguration> editor =
                _activeEditor as MPWConfigEditorControl<MCNConnectorConfiguration>;
            if (editor == null) throw new InvalidOperationException("当前不是Connector正式配置");
            return editor.Draft;
        }
        public string StatusMessage { get { return _status.Text; } }
        public void SelectModuleKind(MFMModuleKind kind)
        {
            if (_opening || _busy) throw new InvalidOperationException("当前模块操作尚未结束");
            if (!Enum.IsDefined(typeof(MFMModuleKind), kind))
                throw new ArgumentOutOfRangeException("kind");
            _moduleKind.SelectedItem = kind;
            _center.SetSelectedKind(kind);
            UpdateStatus("选择 " + kind + "，请创建/打开相应正式配置；已有草稿不会自动丢弃。");
        }
        public void SetModuleId(string id)
        {
            if (_opening || _busy) throw new InvalidOperationException("当前模块操作尚未结束");
            _moduleId.Text = RequireId(id);
        }

        public System.Collections.Generic.IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                return new [] {
                    new TCTWorkspaceAction("module.open", "创建 / 打开模块配置", OpenSelected,
                        () => !IsBusy),
                    new TCTWorkspaceAction("module.validate", "验证当前模块配置", ValidateCurrent,
                        () => HasOpenedConfiguration && !IsBusy),
                    new TCTWorkspaceAction("module.save", "校验并保存", SaveCurrent,
                        () => HasOpenedConfiguration && !IsBusy),
                    new TCTWorkspaceAction("module.apply", "申请在下一任务边界生效", ApplyCurrent,
                        () => HasOpenedConfiguration && !IsBusy),
                    new TCTWorkspaceAction("module.reopen", "重开已保存配置", ReopenCurrent,
                        () => HasOpenedConfiguration && !IsBusy && (_draftGuard == null || !_draftGuard.HasUnsavedDraft))

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
            _service.RegisterAssembly(typeof(MFMDriverModuleConfiguration).Assembly);
            _service.RegisterAssembly(typeof(MCNConnectorConfiguration).Assembly);
            _service.RegisterAssembly(typeof(PVCComputeModuleConfiguration).Assembly);
            _service.RegisterAssembly(typeof(PSMSimulationModuleConfiguration).Assembly);
            _service.RegisterAssembly(typeof(MLCServiceModuleConfiguration).Assembly);
            _moduleKind.ItemsSource = Enum.GetValues(typeof(MFMModuleKind));
            _moduleKind.SelectedItem = MFMModuleKind.Connector;
            _pages.SelectionChanged += (sender, args) => {
                if (ReferenceEquals(args.OriginalSource, _pages))
                    ProjectSelectedPage();
            };

            _center.ModuleKindRequested+=kind=>{
                if(IsBusy||HasUnsavedDraft){
                    UpdateStatus("模块忙或草稿未保存，不能切换类型。");
                    return;
                }
                SelectModuleKind(kind);
            };
            _pages.SetResourceReference(Control.BackgroundProperty,"MPWSurfaceBrush");
            _status.SetResourceReference(TextBlock.ForegroundProperty,"MPWMutedBrush");
            var header=new StackPanel();
            var picker=new WrapPanel {Orientation=Orientation.Horizontal};
            picker.Children.Add(new TextBlock {Text="模块类型",VerticalAlignment=VerticalAlignment.Center});
            picker.Children.Add(_moduleKind);
            picker.Children.Add(new TextBlock {Text="模块 Id",VerticalAlignment=VerticalAlignment.Center});
            picker.Children.Add(_moduleId);
            header.Children.Add(picker);header.Children.Add(_status);
            _center.HeaderContent=header;
            // Preserve the full-width professional FCM editor tested by the original UIA.
            // The same shared MPW center has catalog mode in Studio.
            _center.ConfigurationOnly=true;
            Content=_center;
            _editorHost.Content=new TextBlock{
                Text="右侧操作中心 → 创建 / 打开模块配置",
                Margin=new Thickness(14,22,14,0)};
            RebuildPages(MFMModuleKind.Connector);
            System.Windows.Automation.AutomationProperties.SetAutomationId(_pages,
                "TCT.Module.Pages");
            UpdateStatus("请选择模块类型和 Id，然后创建配置实例。");
            System.Windows.Automation.AutomationProperties.SetAutomationId(this, "TCT.Manual.Module");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_moduleKind, "TCT.Module.Kind");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_moduleId, "TCT.Module.Id");
            System.Windows.Automation.AutomationProperties.SetAutomationId(_status, "TCT.Module.Status");
        }

        private void UpdateStatus(string message)
        {
            _status.Text = message;
            _status.ToolTip = message;
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
            await OpenSelectedAsync(false);
        }
        private async Task OpenSelectedAsync(bool forceReopen)
        {
            if (_opening || _busy) return;
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
                if (!forceReopen && _activeEditor != null && _activeId == id && _activeKind == kind)
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
                        model => model.Kind = kind, "创建五类模块配置身份", CancellationToken.None);
                    if (!result.Success)
                    {
                        UpdateStatus("模块身份提交失败：" + (result.ErrorCode ?? "CONFIG_COMMIT_REJECTED"));
                        return;
                    }
                }
                switch (kind)
                {
                    case MFMModuleKind.Driver:
                        Attach(_service.ForScope("module", id).Open<MFMDriverModuleConfiguration>(), id, kind,
                            model => { model.Validate(); return "Driver 设备驱动配置合法（未启动设备）"; });
                        break;
                    case MFMModuleKind.Connector:
                        Attach(_service.ForScope("module", id).Open<MCNConnectorConfiguration>(), id, kind,
                            model => "Channel " + model.Channel.Id + " / Protocol " + model.Protocol.ProtocolTypeId +
                                " / Points " + model.Protocol.Points.Count +
                                " / Mappings " + model.Protocol.Mappings.Count);
                        break;
                    case MFMModuleKind.Compute:
                        Attach(_service.ForScope("module", id).Open<PVCComputeModuleConfiguration>(), id, kind,
                            model => { model.Validate(); return "Compute 资源配置合法"; });
                        break;
                    case MFMModuleKind.Simulation:
                        Attach(_service.ForScope("module", id).Open<PSMSimulationModuleConfiguration>(), id, kind,
                            model => { model.Validate(); return "Simulation 模型定义合法"; });
                        break;
                    case MFMModuleKind.Service:
                        Attach(_service.ForScope("module", id).Open<MLCServiceModuleConfiguration>(), id, kind,
                            model => { model.Validate(); return "Service 生命周期配置合法"; });
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
            Func<T, string> extra) where T : MFMModuleConfiguration, new()
        {
            MPWConfigEditorControl<T> next = new MPWConfigEditorControl<T>(section);
            // Connector 严格一通道一规约；映射PointRef引用同一正式Protocol点表。
            // 动态候选属于现有MPW的Schema投影，不在TestCenter再建配置选择器。
            if (typeof(T) == typeof(MCNConnectorConfiguration))
                next.ConfigureReferenceChoices(path => {
                    if (!path.StartsWith("Protocol.Mappings[", StringComparison.Ordinal) ||
                        !path.EndsWith("].PointRef", StringComparison.Ordinal))
                        return null;
                    MCNConnectorConfiguration connector = next.Draft as MCNConnectorConfiguration;
                    return connector == null || connector.Protocol == null || connector.Protocol.Points == null
                        ? new string[0] :
                        connector.Protocol.Points.Where(point => point != null)
                            .Select(point => point.Name);
                });
            IDisposable old = _activeEditor;
            _activeEditor = next;
            _draftGuard = next;
            _project = (fields, categories) =>
                next.ConfigureWorkspaceProjection(fields, categories, true);
            _visibleSchemaEditors = () => next.VisibleWorkspaceEditorCount;
            _saveDraft = token => next.SaveDraftAsync(token);
            _applySaved = boundary => next.ApplySaved(boundary);
            _versions = () => "Saved: " + section.Revision.Saved +
                " / Effective: " + section.Revision.Effective;
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
            _refreshDeclaredCapabilities=() => {
                // A declaration projection is not an MOC Provider, Ready or authorization fact.
                var declared=next.Draft.CapabilityIds??
                    new System.Collections.Generic.List<string>();
                _center.SetCapabilities(declared.Where(x=>!String.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .Select(x=>new MPWModuleCapabilityView(x,x,"配置中声明",kind,1,false,false)),
                    "本模块FCM声明的CapabilityIds（尚未核实Provider/授权）");
            };
            _refreshDeclaredCapabilities();
            _editorHost.Content = next;
            RebuildPages(kind);
            if (old != null) old.Dispose();
            UpdateStatus("已创建/打开配置范围 module/" + id + "，类型 " + kind +
                " · " + _versions() + "。通用表单负责草稿、版本；并非已经创建运行实例。");
        }

        public async Task<MHSConfigCommitResult> SaveCurrentAsync()
        {
            if (_saveDraft == null || IsBusy)
                throw new InvalidOperationException("没有可保存的模块配置或当前操作繁忙");
            _busy = true;
            UpdateStatus("正在通过现有 MHS 编辑器验证并提交配置…");
            try
            {
                MHSConfigCommitResult commit = await _saveDraft(CancellationToken.None);
                UpdateStatus(commit.Success
                    ? "配置已保存为 " + commit.Revision + " · " + _versions() +
                      "。未自动装配或启动模块。"
                    : "配置未保存：" + commit.ErrorCode +
                      "；请检查原FCM配置Schema与约束。");
                return commit;
            }
            finally
            {
                _busy = false;
                UpdateStatus(_status.Text);
            }
        }
        private async void SaveCurrent()
        {
            try { await SaveCurrentAsync(); }
            catch (Exception error) { UpdateStatus("保存操作失败：" + error.Message); }
        }
        public void ApplyCurrent()
        {
            if (_applySaved == null || IsBusy) return;
            try
            {
                MHSConfigApplyResult applied = _applySaved(FCMConfigApplyPolicy.NextTask);
                UpdateStatus(applied.Applied
                    ? "配置已按MHS边界生效：" + applied.EffectiveVersion + "；非模块运行成功"
                    : "尚未生效：" + applied.Status + "；运行/任务边界或约束未满足");
            }
            catch (Exception error) { UpdateStatus("MHS生效申请失败：" + error.Message); }
        }
        public async void ReopenCurrent()
        {
            if (_opening || _busy || !HasOpenedConfiguration) return;
            if (_draftGuard != null && _draftGuard.HasUnsavedDraft)
            {
                UpdateStatus("有未保存草稿；先保存或通过原MPW编辑器处理，不允许静默丢弃。");
                return;
            }
            await OpenSelectedAsync(true);
        }

        public void ValidateCurrent()
        {
            if (_validate == null) throw new InvalidOperationException("尚未打开模块配置");
            if (_refreshDeclaredCapabilities != null) _refreshDeclaredCapabilities();
            try { UpdateStatus(_validate()); }
            catch (Exception error) { UpdateStatus("模块结构校验失败：" + error.GetType().Name + "，请检查配置。"); }
        }

        public void OnWorkspaceClosed()
        {
            if (_activeEditor != null) _activeEditor.Dispose();
            _editorHost.Content = null;
            _activeEditor = null; _draftGuard = null; _validate = null;
            _saveDraft = null; _applySaved = null; _versions = null;
            _project = null; _visibleSchemaEditors = null;
            _refreshDeclaredCapabilities = null;
            _center.SetCapabilities(null,"尚未打开正式模块配置");
            _activeId = null; _activeKind = null;
        }
    }
}
