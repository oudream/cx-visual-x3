using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvalonDock.Layout;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Presentation.Wpf;

namespace CxVisual.Studio
{
    /// <summary>
    /// R06a：一个MEM页面对应一个真实SceneView+CxVisualEditor文档。
    /// 不克隆MEM资源、历史、Scene数据；每个Tab的选择和视口独立，编辑事务共用MEM。
    /// </summary>
    public partial class MainWindow
    {
        private const string PageDocumentPrefix = "doc.page.";
        private readonly Dictionary<string, PageDocumentContext> _pageDocuments =
            new Dictionary<string, PageDocumentContext>(StringComparer.Ordinal);
        private bool _switchingPageDocument;
        private bool _closingPageDocuments;
        private MEMEngineeringSession _pageDocumentSession;

        private sealed class PageDocumentContext : IDisposable
        {
            private readonly MainWindow _owner;
            private bool _disposed;
            internal readonly Guid PageId;
            internal readonly MEMEngineeringSession Session;
            internal readonly CxVisualEditor Editor;
            internal readonly SceneView Scene;
            internal readonly DockPanel Panel;
            internal readonly TextBlock Status;

            internal PageDocumentContext(MainWindow owner, MEMEngineeringSession session, MEMPage page)
            {
                _owner = owner;
                Session = session;
                PageId = page.Id;
                Editor = new CxVisualEditor(session);
                Editor.SetPage(PageId);
                Scene = new SceneView { Focusable = true };
                Scene.SetAssetResolver(id => Session.OpenAsset(id));
                Status = new TextBlock { Margin = new Thickness(6, 4, 6, 3), TextWrapping = TextWrapping.Wrap };
                Panel = new DockPanel();
                StackPanel top = new StackPanel { Orientation = Orientation.Vertical };
                WrapPanel actions = new WrapPanel();
                actions.Children.Add(Button("添加矩形", () =>
                {
                    int count = Editor.Scene.Count + 1;
                    MTCEditResult<bool> result = Editor.AddRectangle(
                        "矩形 " + count, 60 + count * 15, 55 + count * 12, 110, 65);
                    Status.Text = result.Success ? "已由MEM提交矩形，支持统一撤销" :
                        "添加失败：" + result.ErrorCode + " " + result.Message;
                }));
                actions.Children.Add(Button("撤销", () =>
                {
                    MTCEditResult<bool> result = Editor.Undo();
                    Status.Text = result.Success ? "已撤销MEM事务" : "撤销失败：" + result.Message;
                }));
                actions.Children.Add(Button("重做", () =>
                {
                    MTCEditResult<bool> result = Editor.Redo();
                    Status.Text = result.Success ? "已重做MEM事务" : "重做失败：" + result.Message;
                }));
                actions.Children.Add(Button("完整图元编辑", () =>
                {
                    MEMPage actual = Session.Find(PageId) as MEMPage;
                    if (actual == null) return;
                    _owner._viewModel.ActivePage = actual;
                    _owner.OpenStudioDocument("doc.visual");
                }));
                top.Children.Add(actions);
                top.Children.Add(Status);
                DockPanel.SetDock(top, Dock.Top);
                Panel.Children.Add(top);
                Panel.Children.Add(Scene);
                Scene.NodeActivated += (sender, args) =>
                {
                    if (_disposed || !_owner.IsStudioDocumentActive(PageDocumentId(PageId))) return;
                    Editor.Select(args.Id);
                    _owner._viewModel.Select(args.Id);
                };
                Scene.SelectionSetRequested += ids =>
                {
                    if (_disposed || !_owner.IsStudioDocumentActive(PageDocumentId(PageId))) return;
                    Guid? selected = ids.Count == 1 ? (Guid?)ids[0] : null;
                    Editor.Select(selected);
                    _owner._viewModel.Select(selected);
                    Scene.SetSelectedVisualIds(ids);
                };
                Scene.MoveRequested += (sender, args) =>
                {
                    if (_disposed || !_owner.IsStudioDocumentActive(PageDocumentId(PageId))) return;
                    MTCEditResult<bool> result = Editor.Move(args.Id, args.X, args.Y);
                    if (!result.Success) Status.Text = "拖动失败：" + result.ErrorCode + " " + result.Message;
                };
                Editor.Changed += OnEditorChanged;
                Refresh();
            }

            private static Button Button(string title, Action action)
            {
                Button b = new Button { Content = title, Padding = new Thickness(9, 5, 9, 5),
                    Margin = new Thickness(2) };
                b.Click += (s, e) => action();
                return b;
            }

            private void OnEditorChanged(object sender, EventArgs args)
            {
                if (_disposed) return;
                if (!_owner.Dispatcher.CheckAccess())
                {
                    _owner.Dispatcher.BeginInvoke(new Action(Refresh));
                    return;
                }
                Refresh();
            }

            internal void Refresh()
            {
                if (_disposed) return;
                MEMPage page = Session.Find(PageId) as MEMPage;
                if (page == null)
                {
                    Status.Text = "工程页面已删除，请关闭此文档";
                    Scene.SetScene(new List<CxSceneItem>(), null);
                    return;
                }
                Scene.SetScene(Editor.Scene, Editor.SelectedId);
                Status.Text = page.Name + " | MEM Revision " + Session.Revision +
                    " | 页面独立视口与选择，Undo/Redo由同一MEM工程持有";
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Editor.Changed -= OnEditorChanged;
                Editor.Dispose();
                Scene.SetAssetResolver(null);
            }
        }

        private static string PageDocumentId(Guid id)
        {
            return PageDocumentPrefix + id.ToString("N");
        }

        private static bool TryParsePageDocumentId(string key, out Guid pageId)
        {
            pageId = Guid.Empty;
            return key != null && key.StartsWith(PageDocumentPrefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(key.Substring(PageDocumentPrefix.Length), "N", out pageId) &&
                pageId != Guid.Empty;
        }

        private PageDocumentContext CreateStudioPageContext(string key)
        {
            Guid id;
            if (!TryParsePageDocumentId(key, out id))
                throw new InvalidDataException("页面文档身份无效");
            MEMPage page = _viewModel.ProjectSession.Find(id) as MEMPage;
            if (page == null)
                throw new InvalidDataException("页面文档属于其他工程或页面已删除：" + id);
            PageDocumentContext existing;
            if (_pageDocuments.TryGetValue(key, out existing)) return existing;
            PageDocumentContext context = new PageDocumentContext(this, _viewModel.ProjectSession, page);
            _pageDocuments.Add(key, context);
            _studioViews[key] = context.Panel;
            return context;
        }

        private void RegisterStudioPageClosing(LayoutDocument document)
        {
            document.Closed += (s, e) =>
            {
                PageDocumentContext context;
                if (_pageDocuments.TryGetValue(document.ContentId, out context))
                {
                    _pageDocuments.Remove(document.ContentId);
                    _studioViews.Remove(document.ContentId);
                    context.Dispose();
                }
                if (_activeStudioDocumentId == document.ContentId && !_closingPageDocuments)
                {
                    SelectStudioContext("doc.visual");
                    RefreshStudioMenus();
                }
            };
        }

        public SceneView GetStudioPageScene(Guid pageId)
        {
            PageDocumentContext context;
            return _pageDocuments.TryGetValue(PageDocumentId(pageId), out context) ? context.Scene : null;
        }

        public string OpenStudioPageDocument(Guid pageId)
        {
            string key = PageDocumentId(pageId);
            if (!(_viewModel.ProjectSession.Find(pageId) is MEMPage))
                throw new InvalidOperationException("无法打开不属于当前工程的页面");
            LayoutDocument existing = FindStudioContent(key) as LayoutDocument;
            if (existing == null)
            {
                PageDocumentContext context = CreateStudioPageContext(key);
                LayoutDocumentPane pane = StudioDock.Layout.Descendents().OfType<LayoutDocumentPane>()
                    .FirstOrDefault();
                if (pane == null) throw new InvalidOperationException("中央工作区已丢失");
                MEMPage page = _viewModel.ProjectSession.Find(pageId) as MEMPage;
                existing = new LayoutDocument { ContentId = key,
                    Title = page.Name + " [设计]", Content = context.Panel,
                    CanClose = true, CanFloat = false };
                RegisterStudioPageClosing(existing);
                pane.Children.Add(existing);
            }
            existing.IsActive = true;
            SelectStudioContext(key);
            RefreshStudioMenus();
            return key;
        }

        private void OnStudioPageSourceChanged(object sender, EventArgs args)
        {
            if (_viewModel == null || _closingPageDocuments) return;
            if (!ReferenceEquals(_pageDocumentSession, _viewModel.ProjectSession))
            {
                _closingPageDocuments = true;
                try
                {
                    foreach (string id in _pageDocuments.Keys.ToArray())
                    {
                        LayoutDocument doc = FindStudioContent(id) as LayoutDocument;
                        if (doc != null) { doc.Content = null; doc.Close(); }
                        PageDocumentContext ctx;
                        if (_pageDocuments.TryGetValue(id, out ctx)) { ctx.Dispose(); _pageDocuments.Remove(id); }
                        _studioViews.Remove(id);
                    }
                    _pageDocumentSession = _viewModel.ProjectSession;
                }
                finally { _closingPageDocuments = false; }
                // 新工程应撤销旧工程页面Tab，但不得将当前固定HMI/流程编辑器强行切回主页。
                // 已关闭的页面Tab则退回通用图元主画布。
                if (_activeStudioDocumentId != null &&
                    _activeStudioDocumentId.StartsWith(PageDocumentPrefix, StringComparison.Ordinal))
                {
                    LayoutDocument fallback = FindStudioContent("doc.visual") as LayoutDocument;
                    if (fallback != null) fallback.IsActive = true;
                    SelectStudioContext("doc.visual");
                    RefreshStudioMenus();
                }
            }
            foreach (string id in _pageDocuments.Keys.ToArray())
            {
                PageDocumentContext ctx = _pageDocuments[id];
                MEMPage page = _viewModel.ProjectSession.Find(ctx.PageId) as MEMPage;
                LayoutDocument doc = FindStudioContent(id) as LayoutDocument;
                if (page == null)
                {
                    if (doc != null) { doc.Content = null; doc.Close(); }
                    continue;
                }
                if (doc != null) doc.Title = page.Name + (_viewModel.IsDirty ? " *" : "") + " [设计]";
            }
        }

    }
}
