using System;
using System.ComponentModel;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Foundation.Infrastructure;

namespace CxVisual.Studio
{
    public sealed class StudioViewModel : INotifyPropertyChanged, IDisposable
    {
        private CxVisualEditor _editor;
        private Guid? _selected;
        private string _status;
        private readonly string _defaultPath;
        private readonly FLDFileLogSink _logger;
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler SceneChanged;
        public StudioViewModel()
        {
            _defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "demo-project.xml");
            _logger = new FLDFileLogSink(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "studio.log"));
            NewCommand = new RelayCommand(CreateNew);
            AddCommand = new RelayCommand(Add);
            MoveCommand = new RelayCommand(MoveSelected, () => _selected.HasValue);
            UndoCommand = new RelayCommand(() => Execute(() => _editor.Undo()));
            RedoCommand = new RelayCommand(() => Execute(() => _editor.Redo()));
            SaveCommand = new RelayCommand(Save);
            LoadCommand = new RelayCommand(Load);
            CreateNew();
        }
        public ICommand NewCommand { get; private set; }
        public ICommand AddCommand { get; private set; }
        public ICommand MoveCommand { get; private set; }
        public ICommand UndoCommand { get; private set; }
        public ICommand RedoCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand LoadCommand { get; private set; }
        public int Revision { get { return _editor.Document.Revision; } }
        public int ElementCount { get { return _editor.Scene.Count; } }
        public string Status { get { return _status; } private set { _status = value; Changed("Status"); } }
        public string SelectedName
        {
            get
            {
                if (!_selected.HasValue) return "（未选中）";
                CxNode node = _editor.Document.Project.Find(_selected.Value);
                return node != null ? node.Name + " / " + node.Id.ToString("N").Substring(0, 8) : "（已删除）";
            }
        }
        public ReadOnlyCollection<CxSceneItem> Items { get { return _editor.Scene; } }
        public Guid? SelectedId { get { return _selected; } }

        public void Select(Guid? id)
        {
            _selected = id;
            Changed("SelectedName");
            FireSceneChanged();
        }
        public void Drag(Guid id, double x, double y)
        {
            Select(id);
            Execute(() => _editor.Move(id, x, y));
        }
        private void CreateNew()
        {
            _editor = new CxVisualEditor(new CxEngineeringSession(CxDocumentFactory.CreateSample()));
            _selected = null;
            _logger.Write(new FLDLogRecord("Engineering", "CreateProject", "New example project"));
            Status = "已建立示例工程；使用鼠标选中/拖动矩形，或点击上方命令。";
            NotifyScene();
        }
        private void Add()
        {
            int index = ElementCount + 1;
            Execute(() => _editor.AddRectangle("Element " + index, 60 + 35 * index, 60 + 25 * index, 110, 65));
        }
        private void MoveSelected()
        {
            if (!_selected.HasValue) return;
            CxElement element = _editor.Document.Project.Find(_selected.Value) as CxElement;
            if (element != null) Execute(() => _editor.Move(element.Id, element.X + 20, element.Y));
        }
        private void Execute(Func<CxCommitResult> callback)
        {
            try
            {
                CxCommitResult result = callback();
                _logger.Write(new FLDLogRecord("Visual", result.Success ? "Committed" : result.ErrorCode, result.Message));
                Status = result.Success ? "操作成功 | Revision " + result.Revision : result.ErrorCode + ": " + result.Message;
                NotifyScene();
            }
            catch (Exception ex) { Status = "执行失败: " + ex.Message; }
        }
        private void Save()
        {
            try { CxDocumentSerializer.Save(_editor.Document, _defaultPath); Status = "已保存: " + _defaultPath; }
            catch (Exception ex) { Status = "保存失败: " + ex.Message; }
        }
        private void Load()
        {
            try
            {
                CxProjectDocument project = CxDocumentSerializer.Load(_defaultPath);
                _editor = new CxVisualEditor(new CxEngineeringSession(project));
                _selected = null;
                Status = "已加载: " + _defaultPath;
                NotifyScene();
            }
            catch (Exception ex) { Status = "加载失败: " + ex.Message; }
        }
        private void NotifyScene()
        {
            Changed("Revision"); Changed("ElementCount"); Changed("SelectedName");
            FireSceneChanged();
            CommandManager.InvalidateRequerySuggested();
        }
        private void FireSceneChanged()
        {
            EventHandler callback = SceneChanged;
            if (callback != null) callback(this, EventArgs.Empty);
        }
        public void Dispose() { _logger.Dispose(); }
        private void Changed(string property)
        {
            PropertyChangedEventHandler callback = PropertyChanged;
            if (callback != null) callback(this, new PropertyChangedEventArgs(property));
        }
    }
}
