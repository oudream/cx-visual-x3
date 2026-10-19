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
        /// <summary>路径、缩放和旋转统一提交到MEM事务；失败时不修改工程状态。</summary>
        public MTCEditResult<bool> EditVisualHandle(MVIVisualHandle handle,double worldX,double worldY)
        {
            if(handle==null)throw new ArgumentNullException("handle");
            MTCEditResult<bool> result=_editor.EditHandle(handle,worldX,worldY);
            _logger.Write(new FLDLogRecord("Visual",
                result.Success?"Committed":result.ErrorCode,result.Message));
            Status=result.Success?"修改图元成功 | Revision "+result.Revision:
                result.ErrorCode+": "+result.Message;
            if(result.Success)NotifyScene();
            return result;
        }
        public MTCEditResult<bool> AlignVisualSelection(string mode)
        {
            MTCEditResult<bool> result=_editor.Align(_selection,mode);
            Status=result.Success?"图元对齐/分布已提交":"无法对齐："+result.ErrorCode+" "+result.Message;
            if(result.Success)NotifyScene();
            return result;
        }
        public MTCEditResult<bool> EditVisualGeometry(double x,double y,double width,
            double height,double rotation)
        {
            if(!_selected.HasValue)throw new InvalidOperationException("请选择图元定义");
            MTCEditResult<bool> result=_editor.SetGeometry(_selected.Value,x,y,width,height,rotation);
            Status=result.Success?"几何属性已保存："+result.Revision:result.ErrorCode+": "+result.Message;
            if(result.Success)NotifyScene();
            return result;
        }
        public MTCEditResult<bool> EditVisualPath(int index,string operation)
        {
            if(!_selected.HasValue)throw new InvalidOperationException("请选择路径图元");
            MTCEditResult<bool> result=_editor.EditPathSegment(_selected.Value,index,operation);
            Status=result.Success?"路径操作已保存："+result.Revision:result.ErrorCode+": "+result.Message;
            if(result.Success)NotifyScene();
            return result;
        }
        public MTCEditResult<bool> EditVisualLayer(string layer,bool toFront)
        {
            if(!_selected.HasValue)throw new InvalidOperationException("请选择图元");
            MTCEditResult<bool> result=_editor.SetLayer(_selected.Value,layer,toFront);
            Status=result.Success?"图层与顺序已保存："+result.Revision:result.ErrorCode+": "+result.Message;
            if(result.Success)NotifyScene();
            return result;
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
        /// <summary>导入项目打包资产并在当前编辑资源创建真实图片/SVG图元。</summary>
        public void AddVisualAsset(string file)
        {
            if(string.IsNullOrWhiteSpace(file)||!File.Exists(file))
                throw new FileNotFoundException("请选择存在的本地资源文件",file);
            string extension=Path.GetExtension(file).ToLowerInvariant();
            string media=extension==".svg"?"image/svg+xml":
                extension==".png"?"image/png":extension==".jpg"||extension==".jpeg"?
                "image/jpeg":extension==".bmp"?"image/bmp":null;
            if(media==null)throw new InvalidOperationException("只支持PNG/JPEG/BMP及受限SVG");
            MEMAssetReference asset=ProjectSession.AddAsset(Path.GetFileName(file),file,media);
            int before=ElementCount;
            Execute(()=>_editor.AddAssetElement(asset.Id,Path.GetFileNameWithoutExtension(file),
                extension==".svg",60+before*11,90+before*8));
            if(ElementCount>before)Select(Items[Items.Count-1].Id);
        }
        public void AddVisualWidgetType(string type)
        {
            if(type!="core.widget"&&type!="core.widget.textbox"&&
                type!="core.widget.checkbox"&&type!="core.widget.slider"&&
                type!="core.widget.progressbar"&&type!="core.widget.combobox")
                throw new ArgumentException("Unsupported Widget type", "type");
            int before=ElementCount;
            Execute(()=>_editor.AddWidget(type.Substring(type.LastIndexOf('.')+1)+" "+before,
                type,65+before*17,60+before*12));
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
        private CxVisual.Mechanism.Data.RuntimeData.IMDSExternalBindingResolver _visualRuntimeSource;
        /// <summary>生产运行数据由现有MDS/专业Owner提供；Studio不建立第二份变量表。</summary>
        public void AttachVisualRuntimeSource(
            CxVisual.Mechanism.Data.RuntimeData.IMDSExternalBindingResolver source)
        {
            _visualRuntimeSource=source;
        }
        public void UpdateVisualRuntimeFrame(double elapsedMilliseconds)
        {
            _editor.SetRuntimeFrame(_visualRuntimeSource,elapsedMilliseconds);
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
        private MRCVisualItem SelectedVisualDefinition()
        {
            if(!_selected.HasValue)throw new InvalidOperationException("请选择图元定义");
            MRCVisualItem item=ProjectSession.Find(_selected.Value) as MRCVisualItem;
            if(item==null)throw new InvalidOperationException("组件引用请先打开定义进行修改");
            return item;
        }
        /// <summary>六类WPF Widget共享MPM定义；一次提交内容/选项/范围/状态，撤销只需一步。</summary>
        public MTCEditResult<bool> ConfigureVisualWidget(string content,string options,
            double minimum,double maximum,bool enabled,string tooltip)
        {
            MRCWidgetElement widget=SelectedVisualDefinition() as MRCWidgetElement;
            if(widget==null)throw new InvalidOperationException("请选择Widget图元");
            if(content==null||options==null||tooltip==null||
                content.Length>8192||options.Length>8192||tooltip.Length>2048||
                double.IsNaN(minimum)||double.IsNaN(maximum)||
                double.IsInfinity(minimum)||double.IsInfinity(maximum)||
                minimum>maximum||options.Split('|').Length>128)
                throw new ArgumentException("Widget配置范围错误");
            int revision=ProjectSession.Revision;
            MTCEditResult<bool> result=ProjectSession.Edit("配置可视控件",revision,e=>
            {
                e.SetBinding(widget,"content",MPMBinding.Literal(MPMValue.String(content)));
                e.SetBinding(widget,"options",MPMBinding.Literal(MPMValue.String(options)));
                e.SetBinding(widget,"minimum",MPMBinding.Literal(MPMValue.Number(minimum)));
                e.SetBinding(widget,"maximum",MPMBinding.Literal(MPMValue.Number(maximum)));
                e.SetBinding(widget,"enabled",MPMBinding.Literal(MPMValue.Boolean(enabled)));
                e.SetBinding(widget,"tooltip",MPMBinding.Literal(MPMValue.String(tooltip)));
                return true;
            });
            Status=result.Success?"Widget配置已保存 | Revision "+result.Revision:
                result.ErrorCode+": "+result.Message;
            if(result.Success)NotifyScene();
            return result;
        }
        public void BindVisualVariable(string property,string variable)
        {
            if(property!="visible"&&property!="x"&&property!="y"&&
                property!="width"&&property!="height"&&property!="rotation"&&
                property!="scaleX"&&property!="scaleY"&&property!="shear"&&
                property!="value"&&property!="locked"&&property!="selectable")
                throw new ArgumentException("不支持的动态属性", "property");
            MRCVisualItem item=SelectedVisualDefinition();
            ProjectSession.SetBinding(item,property,MPMBinding.Variable(variable));
            Status="MDS变量绑定已保存："+property+" ← "+variable;
            NotifyScene();
        }
        public void SetFullVisualStyle(string strokeColor,double opacity,double fontSize)
        {
            MRCVisualItem item=SelectedVisualDefinition();
            MRCVisualStyle old=MRCVisualAppearance.StyleOf(item);
            MRCVisualStyle style=new MRCVisualStyle(old.Fill,
                MRCBrushDefinition.Solid(MRCColor.Parse(strokeColor)),
                old.StrokeWidth,opacity,old.FontFamily,fontSize,old.ThemeKey);
            ProjectSession.SetVisualStyle(item,style);
            Status="完整样式已保存";
            NotifyScene();
        }
        public void AddVisualState(string stateName,string fillColor)
        {
            MRCVisualItem item=SelectedVisualDefinition();
            MRCVisualStyle old=MRCVisualAppearance.StyleOf(item);
            MRCVisualStyle style=new MRCVisualStyle(
                MRCBrushDefinition.Solid(MRCColor.Parse(fillColor)),old.Stroke,
                old.StrokeWidth,old.Opacity,old.FontFamily,old.FontSize,old.ThemeKey);
            MRCVisualBehaviorDefinition before=MRCVisualAppearance.BehaviorOf(item);
            var states=new System.Collections.Generic.List<MRCVisualStateDefinition>(before.States);
            states.RemoveAll(x=>x.Name==stateName);
            states.Add(new MRCVisualStateDefinition(stateName,style));
            ProjectSession.SetVisualBehavior(item,new MRCVisualBehaviorDefinition(
                states,before.Animations,before.EventActions));
            Status="状态定义已保存；通过现有MDS布尔变量触发："+stateName;
            NotifyScene();
        }
        public void AddVisualAnimation(string property,double from,double to,int duration)
        {
            if(property!="opacity"&&property!="strokeWidth"&&property!="x"&&
                property!="y"&&property!="width"&&property!="height"&&
                property!="rotation"&&property!="scaleX"&&property!="scaleY"&&
                property!="shear")throw new ArgumentException("不支持的动画属性","property");
            MRCVisualItem item=SelectedVisualDefinition();
            MRCVisualBehaviorDefinition before=MRCVisualAppearance.BehaviorOf(item);
            var animations=new System.Collections.Generic.List<MRCAnimationDefinition>(before.Animations);
            animations.RemoveAll(x=>x.Property==property);
            animations.Add(new MRCAnimationDefinition(property,from,to,duration,"",
                MRCAnimationEasing.EaseInOut,true));
            ProjectSession.SetVisualBehavior(item,new MRCVisualBehaviorDefinition(
                before.States,animations,before.EventActions));
            Status="动画已保存："+property+" ("+duration+"ms)";
            NotifyScene();
        }
        private Func<MVIVisualActionInvocation,bool> _authorizedVisualActionHost;
        /// <summary>只能由专业运行宿主显式注入；Studio预览默认不具备设备控制权限。</summary>
        public void AttachVisualActionHost(Func<MVIVisualActionInvocation,bool> host)
        {
            _authorizedVisualActionHost=host;
        }
        public int RouteVisualWidgetEvent(MRIObjectAddress address,string eventId,string value)
        {
            int approved=MVIVisualEventRouter.Dispatch(ProjectSession,address,eventId,
                value,_authorizedVisualActionHost);
            Status=approved>0?"已通过专业授权宿主转发 "+approved+" 个动作":
                "事件收到，但未授权执行设备命令";
            return approved;
        }
        public int RouteVisualWidgetEvent(Guid visualId,string eventId,string value)
        {
            int approved=MVIVisualEventRouter.Dispatch(ProjectSession,visualId,eventId,
                value,_authorizedVisualActionHost);
            Status=approved>0?"已通过专业授权宿主转发 "+approved+" 个动作":
                "事件收到，但未授权执行设备命令";
            return approved;
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
