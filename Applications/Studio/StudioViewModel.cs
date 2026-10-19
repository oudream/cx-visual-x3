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
    public sealed partial class StudioViewModel : INotifyPropertyChanged, IDisposable
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
            _defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "demo-project.cxproj");
            _logger = new FLDFileLogSink(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "studio.log"));
            NewCommand = new RelayCommand(CreateNew);
            AddCommand = new RelayCommand(Add);
            MoveCommand = new RelayCommand(MoveSelected, () => _selected.HasValue);
            UndoCommand = new RelayCommand(() => Execute(() => _editor.Undo()));
            RedoCommand = new RelayCommand(() => Execute(() => _editor.Redo()));
            SaveCommand = new RelayCommand(Save);
            LoadCommand = new RelayCommand(Load);
            InitializeEngineering();
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
                MEMNode node = _editor.Document.Find(_selected.Value);
                return node != null ? node.Name + " / " + node.Id.ToString("N").Substring(0, 8) : "（已删除）";
            }
        }
        public ReadOnlyCollection<CxSceneItem> Items { get { return _editor.Scene; } }
        public Guid? SelectedId { get { return _selected; } }

        public void Select(Guid? id)
        {
            if(!_editor.Select(id))return;
            _selected = id;
            _selection=id.HasValue?new[]{id.Value}:new Guid[0];
            NotifyScene();
        }
        public void Drag(Guid id, double x, double y)
        {
            Select(id);
            Execute(() => _editor.Move(id, x, y));
        }
        private static MEMEngineeringSession CreateSample()
        {
            var session = MEMEngineering.Create("Demo Project");
            var page = session.AddPage("Main Page");
            var group = session.AddGroup(page.RootContent, "Drawing Group");
            session.AddRectangle(group, "Detector", 50, 65, 170, 100);
            session.AddRectangle(group, "Source", 300, 180, 125, 75);
            session.ClearHistory();
            return session;
        }
        private void CreateNew()
        {
            if(!CanReplace())return;AttachSession(CreateSample());
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
        /// <summary>Studio工具箱统一创建九种Shape；当前页面或Symbol资源是唯一工程真源。</summary>
        public void AddVisualShape(MRCShapeKind kind)
        {
            int index=ElementCount+1;
            MRCShapeDefinition definition=MVIShapeCatalog.CreateDefault(kind);
            int before=ElementCount;
            Execute(()=>_editor.AddShape(kind+" "+index,definition,
                45+(index%7)*24,55+(index%7)*20));
            if(ElementCount>before)Select(Items[Items.Count-1].Id);
        }
        public void AddVisualWidget()
        {
            int index=ElementCount+1,before=ElementCount;
            Execute(()=>_editor.AddWidget("Button "+index,"core.widget",
                45+(index%7)*25,55+(index%7)*20));
            if(ElementCount>before)Select(Items[Items.Count-1].Id);
        }
        public void AddVisualText()
        {
            int index=ElementCount+1;
            int before=ElementCount;
            Execute(()=>_editor.AddText("文字 "+index,"双击属性面板修改",45+index*14,40+index*10));
            if(ElementCount>before)Select(Items[Items.Count-1].Id);
        }
        public void SetSelectedFillColor(string color,double strokeWidth)
        {
            if(!_selected.HasValue)throw new InvalidOperationException("请选择需要设置样式的图元");
            MRCVisualItem item=ProjectSession.Find(_selected.Value) as MRCVisualItem;
            if(item==null)throw new InvalidOperationException("组件实例请先打开定义进行样式编辑");
            MRCColor fill=MRCColor.Parse(color);
            MRCVisualStyle old=MRCVisualAppearance.StyleOf(item);
            var style=new MRCVisualStyle(MRCBrushDefinition.Solid(fill),old.Stroke,
                strokeWidth,old.Opacity,old.FontFamily,old.FontSize,old.ThemeKey);
            ProjectSession.SetVisualStyle(item,style);
            Status="已应用图元填充和线宽 | Revision "+ProjectSession.Revision;
            NotifyScene();
        }
        /// <summary>真正写入工程动画定义；呈现帧由MVI运行态求值，不修改工程数据。</summary>
        public void AddSelectedBreathingAnimation()
        {
            if(!_selected.HasValue)throw new InvalidOperationException("请选择图元");
            MRCVisualItem item=ProjectSession.Find(_selected.Value) as MRCVisualItem;
            if(item==null)throw new InvalidOperationException("请选择图元定义（实例请打开共享定义）");
            var old=MRCVisualAppearance.BehaviorOf(item);
            var animations=new System.Collections.Generic.List<MRCAnimationDefinition>(old.Animations);
            animations.Add(new MRCAnimationDefinition("opacity",0.15,1,900,"",
                MRCAnimationEasing.EaseInOut,true));
            ProjectSession.SetVisualBehavior(item,new MRCVisualBehaviorDefinition(
                old.States,animations,old.EventActions));
            Status="已添加透明度呼吸动画；预览交互可查看真实动画";
            NotifyScene();
        }
        public void UpdateVisualRuntimeFrame(double elapsedMilliseconds)
        {
            _editor.SetRuntimeFrame(null,elapsedMilliseconds);
            // 运行帧只更新Scene，不刷新属性面板、资源树或工程历史。
            Changed("Items");
            FireSceneChanged();
        }
        public void StopVisualRuntime()
        {
            _editor.ClearRuntimeFrame();
            Changed("Items");
            FireSceneChanged();
        }
        public void AppendSelectedEventAction(string eventId,string actionId)
        {
            if(!_selected.HasValue)throw new InvalidOperationException("请选择图元");
            MRCVisualItem item=ProjectSession.Find(_selected.Value) as MRCVisualItem;
            if(item==null)throw new InvalidOperationException("组件实例请先打开定义");
            var old=MRCVisualAppearance.BehaviorOf(item);
            var actions=new System.Collections.Generic.List<MRCVisualEventAction>(old.EventActions);
            actions.Add(new MRCVisualEventAction(eventId,actionId));
            ProjectSession.SetVisualBehavior(item,new MRCVisualBehaviorDefinition(
                old.States,old.Animations,actions));
            Status="已保存事件动作定义（实际命令仍由运行时验证）";
            NotifyScene();
        }
        private void MoveSelected()
        {
            if (!_selected.HasValue) return;
            MRCVisualItem element = _editor.Document.Find(_selected.Value) as MRCVisualItem;
            if (element != null) Execute(() => _editor.Move(element.Id, element.X + 20, element.Y));
        }
        private void Execute(Func<MTCEditResult<bool>> callback)
        {
            try
            {
                MTCEditResult<bool> result = callback();
                _logger.Write(new FLDLogRecord("Visual", result.Success ? "Committed" : result.ErrorCode, result.Message));
                Status = result.Success ? "操作成功 | Revision " + result.Revision : result.ErrorCode + ": " + result.Message;
                NotifyScene();
            }
            catch (Exception ex) { Status = "执行失败: " + ex.Message; }
        }
        private void Save()
        {
            try
            {
                string path=_editor.Document.Location;if(path==null){var dialog=new Microsoft.Win32.SaveFileDialog{Filter="CxVisual 工程|*.cxproj",FileName="project.cxproj"};if(dialog.ShowDialog()!=true)return;path=dialog.FileName;}
                SaveProject(path);
            }
            catch (Exception ex) { Status = "保存失败: " + ex.Message; }
        }
        private void Load()
        {
            try
            {
                var dialog=new Microsoft.Win32.OpenFileDialog{Filter="CxVisual 工程|*.cxproj"};if(dialog.ShowDialog()!=true)return;if(!CanReplace())return;OpenProject(dialog.FileName,true);
            }
            catch (Exception ex) { Status = "加载失败: " + ex.Message; }
        }
        private void NotifyScene()
        {
            if(_notifying)return;_notifying=true;
            try{RefreshEngineering();Changed("Revision");Changed("ElementCount");Changed("SelectedName");Changed("Items");FireSceneChanged();CommandManager.InvalidateRequerySuggested();}
            finally{_notifying=false;}
        }
        private void FireSceneChanged()
        {
            EventHandler callback = SceneChanged;
            if (callback != null) callback(this, EventArgs.Empty);
        }
        public void Dispose() { if(_editor!=null){_editor.Dispose();_editor.Document.Close(true);}_logger.Dispose(); }
        private void Changed(string property)
        {
            PropertyChangedEventHandler callback = PropertyChanged;
            if (callback != null) callback(this, new PropertyChangedEventArgs(property));
        }
    }
}
