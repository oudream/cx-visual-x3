using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Presentation.Wpf.Volume;
using CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering;
using CxVisual.Professional.CT3D;
using Microsoft.Win32;

namespace CxVisual.Studio
{
    /// <summary>正式Studio CT3D MEM工程文档：复用MPW四视图及CT3D原值专业API。
    /// 每个打开的工程有一个MEM会话；不复制工程树、算法、Undo或原始Volume真值。</summary>
    public sealed class StudioCT3DDocument : Grid, IDisposable
    {
        private readonly MPWImageViewerControl _xy=new MPWImageViewerControl();
        private readonly MPWImageViewerControl _xz=new MPWImageViewerControl();
        private readonly MPWImageViewerControl _yz=new MPWImageViewerControl();
        private readonly MPWVolumeSliceSceneControl _scene=new MPWVolumeSliceSceneControl();
        private readonly MPWVolumeSceneObjectListControl _objects=new MPWVolumeSceneObjectListControl();
        private readonly MPWVolumeFourViewWorkbenchControl _workbench;
        private readonly ComboBox _cases=new ComboBox {MinWidth=170,Margin=new Thickness(4)};
        private readonly TextBox _minimum=new TextBox {Text="0.49",Width=70,Margin=new Thickness(3)};
        private readonly TextBox _maximum=new TextBox {Text="0.51",Width=70,Margin=new Thickness(3)};
        private readonly TextBlock _status=new TextBlock {TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(6)};
        private MEMEngineeringSession _engineering;
        private MEMContent[] _owners;
        private string _projectPath,_root;
        private FVOVolumeData _volume;
        private MVIVolumeViewState _state;
        private CT3DAnalysisCase _current,_pending;
        private int _caseIndex=-1;
        private bool _updating,_disposed;

        public StudioCT3DDocument()
        {
            AutomationProperties.SetAutomationId(this,"Studio.CT3D.Document");
            RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});
            ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(245)});
            var toolbar=new StackPanel {Orientation=Orientation.Horizontal};
            var open=Button("打开CT3D工程","Studio.CT3D.Open");
            open.Click+=(s,e)=>OpenWithDialog();
            toolbar.Children.Add(open);
            _cases.SelectionChanged+=(s,e)=>{
                if(!_updating&&_cases.SelectedIndex>=0)TryAction(()=>SelectCase(_cases.SelectedIndex));
            };
            toolbar.Children.Add(_cases);
            toolbar.Children.Add(new TextBlock {Text="ROI法向偏移(mm)",VerticalAlignment=VerticalAlignment.Center});
            toolbar.Children.Add(_minimum);toolbar.Children.Add(_maximum);
            var analyze=Button("ROI重算","Studio.CT3D.AnalyzeRoi");
            analyze.Click+=(s,e)=>TryAction(AnalyzeSelectedRoi);
            toolbar.Children.Add(analyze);
            var save=Button("保存专业结果","Studio.CT3D.Save");
            save.Click+=(s,e)=>TryAction(SaveResult);
            toolbar.Children.Add(save);
            SetRow(toolbar,0);SetColumnSpan(toolbar,2);Children.Add(toolbar);
            var host=new Grid();
            host.Children.Add(_scene);
            _workbench=new MPWVolumeFourViewWorkbenchControl(_xy,_yz,_xz,host,
                ()=>_scene.ResetCamera(),steps=>_scene.Zoom(steps),
                (dx,dy)=>_scene.Pan(dx,dy));
            _workbench.PhysicalRoiSelected+=roi=>{
                _pending=null;
                _status.Text=String.Format(CultureInfo.InvariantCulture,
                    "{0} ROI: ({1:0.###},{2:0.###},{3:0.###}) mm → ({4:0.###},{5:0.###},{6:0.###}) mm",
                    roi.Plane,roi.TopLeft.X,roi.TopLeft.Y,roi.TopLeft.Z,
                    roi.BottomRight.X,roi.BottomRight.Y,roi.BottomRight.Z);
            };
            _workbench.VoxelInspected+=(s,e)=>_status.Text=String.Format(
                CultureInfo.InvariantCulture,"Voxel ({0},{1},{2}) = {3:0.###}; mm ({4:0.###},{5:0.###},{6:0.###})",
                e.X,e.Y,e.Z,e.Value,e.PhysicalX,e.PhysicalY,e.PhysicalZ);
            SetRow(_workbench,1);Children.Add(_workbench);
            var side=new DockPanel();
            SetRow(side,1);SetColumn(side,1);Children.Add(side);
            DockPanel.SetDock(_status,Dock.Bottom);
            side.Children.Add(_status);
            var scroll=new ScrollViewer {VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                Content=_objects};
            side.Children.Add(scroll);
            _status.Text="打开含CT3D正式Data Aspect的MEM工程，不载入演示体积。";
        }

        private static Button Button(string title,string id)
        {
            var button=new Button {Content=title,Padding=new Thickness(6,3,6,3),
                Margin=new Thickness(3)};
            AutomationProperties.SetAutomationId(button,id);
            return button;
        }
        public bool HasPendingAnalysis {get{return _pending!=null;}}
        public int CaseCount {get{return _owners==null?0:_owners.Length;}}
        public CT3DAnalysisCase CurrentAnalysis {get{return _pending??_current;}}
        public string ProjectPath {get{return _projectPath;}}
        public MPWVolumeFourViewWorkbenchControl Workbench {get{return _workbench;}}

        private void TryAction(Action action)
        {
            try{action();}
            catch(Exception ex){_status.Text="三维工作台操作失败："+ex.Message;}
        }
        private void OpenWithDialog()
        {
            if(HasPendingAnalysis)
            {
                _status.Text="当前专业结果尚未保存，请先保存或重新打开本工作台文档。";
                return;
            }
            var dialog=new OpenFileDialog {Title="打开真实CT3D MEM工程",
                Filter="CxVisual工程 (*.cxproj)|*.cxproj"};
            if(dialog.ShowDialog()==true)TryAction(()=>OpenProject(dialog.FileName));
        }

        public void OpenProject(string projectFile)
        {
            if(_disposed)throw new ObjectDisposedException("StudioCT3DDocument");
            if(HasPendingAnalysis)
                throw new InvalidOperationException("当前ROI分析未保存，拒绝切换工程。");
            if(string.IsNullOrWhiteSpace(projectFile))throw new ArgumentException("工程文件路径为空。");
            string full=Path.GetFullPath(projectFile);
            MEMEngineeringSession next=MEMEngineering.Open(full);
            try
            {
                MEMContent[] owners=next.Project.DescendantsAndSelf().OfType<MEMContent>()
                    .Where(c=>c.Aspects.Any(a=>a.Kind==MPMAspectKind.Data&&
                        a.ProviderId==CT3DAnalysisPersistence.ProviderId)).ToArray();
                if(owners.Length==0)throw new InvalidDataException("工程内没有已验证的CT3D专业Case。");
                if(owners.Length>128)throw new InvalidDataException("工程超过128个三维专业案例预算。");
                // 提交新工程前验证第一个真实Case与RAW Hash。
                var verified=CT3DAnalysisPersistence.Open(next,next.GetAddress(owners[0]),
                    Path.GetDirectoryName(full),CancellationToken.None);
                DisposeCurrent();
                _engineering=next;
                _owners=owners;_root=Path.GetDirectoryName(full);_projectPath=full;
                next=null;
                _updating=true;
                try
                {
                    _cases.Items.Clear();
                    for(int i=0;i<_owners.Length;i++)_cases.Items.Add(
                        (i+1).ToString(CultureInfo.InvariantCulture)+" · "+_owners[i].Name);
                    _cases.SelectedIndex=0;
                }
                finally{_updating=false;}
                DisplayCase(verified,0);
            }
            finally{if(next!=null)next.Close(true);}
        }

        private void SelectCase(int index)
        {
            if(_engineering==null||_owners==null)throw new InvalidOperationException("没有打开CT3D工程。");
            if(index<0||index>=_owners.Length)throw new ArgumentOutOfRangeException("index");
            if(HasPendingAnalysis)
                throw new InvalidOperationException("当前ROI分析未保存，不能切换专业Case。");
            CT3DAnalysisCase analysis=CT3DAnalysisPersistence.Open(_engineering,
                _engineering.GetAddress(_owners[index]),_root,CancellationToken.None);
            DisplayCase(analysis,index);
        }

        private void DisplayCase(CT3DAnalysisCase analysis,int index)
        {
            FVOVolumeData volume=analysis.Source.LoadVolume(_root,CancellationToken.None);
            try
            {
                if(_state!=null)_state.PropertyChanged-=OnVolumeStateChanged;
                _workbench.Bind(null,null);
                _objects.Bind(null);
                _scene.Clear();
                if(_volume!=null)_volume.Dispose();
                _volume=volume;volume=null;
                _state=MVIVolumeViewState.CreateCentered(_volume);
                _scene.BindCamera(_state.Camera);
                _workbench.Bind(_volume,_state);
                _state.PropertyChanged+=OnVolumeStateChanged;
                _current=analysis;_pending=null;_caseIndex=index;
                BuildSceneReferences(analysis,false);
                if(_owners[index].Aspects.Any(a=>a.Kind==MPMAspectKind.Visual&&
                    a.ProviderId==MVI3DSceneLayoutPersistence.ProviderId))
                {
                    MVI3DSceneLayoutPersistence.Open(_engineering,
                        _engineering.GetAddress(_owners[index]),_state.Scene3D,
                        item=>IsCurrentReference(item,analysis),CancellationToken.None);
                }
                _objects.Bind(_state.Scene3D);
                _state.Scene3D.Changed+=OnSceneChanged;
                RefreshSlices();
                RefreshProfessionalOverlay();
                _status.Text="已验证原值和专业工程："+
                    analysis.Kind+" · "+analysis.GetFeatureMeasurements().Count+"项量值。";
            }
            finally{if(volume!=null)volume.Dispose();}
        }

        private static bool IsCurrentReference(MVI3DSceneItem item,CT3DAnalysisCase analysis)
        {
            if(item.SourceRevision!=analysis.Source.Revision||
                item.SpatialFrameRef!=analysis.Source.Signature)return false;
            if(item.Kind==MVI3DSceneKind.Volume)return item.SourceRef==analysis.Source.SourceRef;
            if(item.Kind==MVI3DSceneKind.Mesh&&analysis.Kind==CT3DAnalysisKind.MeshPlane)
                return item.SourceRef=="ct3d:mesh:"+analysis.Mesh.ResultId.ToString("N");
            if(item.Kind==MVI3DSceneKind.Surface&&analysis.Kind!=CT3DAnalysisKind.MeshPlane)
                return item.SourceRef=="ct3d:surface:"+analysis.Surface.ResultId.ToString("N");
            if(item.Kind!=MVI3DSceneKind.Measurement)return false;
            Guid id=analysis.Kind==CT3DAnalysisKind.MeshPlane?analysis.Plane.ResultId:
                analysis.Kind==CT3DAnalysisKind.SurfaceSphere?analysis.Sphere.ResultId:
                analysis.Cylinder.ResultId;
            string kind=analysis.Kind==CT3DAnalysisKind.MeshPlane?"plane":
                analysis.Kind==CT3DAnalysisKind.SurfaceSphere?"sphere":"cylinder";
            return item.SourceRef=="ct3d:"+kind+":"+id.ToString("N");
        }

        private void BuildSceneReferences(CT3DAnalysisCase analysis,bool preserveStyles)
        {
            var scene=_state.Scene3D;
            var old=preserveStyles?scene.Snapshot():null;
            MVI3DSceneKind shape=analysis.Kind==CT3DAnalysisKind.MeshPlane?
                MVI3DSceneKind.Mesh:MVI3DSceneKind.Surface;
            scene.Clear();
            scene.Add(MVI3DSceneKind.Volume,analysis.Source.SourceRef,
                analysis.Source.Revision,analysis.Source.Signature);
            string shapeRef=shape==MVI3DSceneKind.Mesh?
                "ct3d:mesh:"+analysis.Mesh.ResultId.ToString("N"):
                "ct3d:surface:"+analysis.Surface.ResultId.ToString("N");
            Guid shapeId=scene.Add(shape,shapeRef,analysis.Source.Revision,analysis.Source.Signature);
            Guid feature=analysis.Kind==CT3DAnalysisKind.MeshPlane?analysis.Plane.ResultId:
                analysis.Kind==CT3DAnalysisKind.SurfaceSphere?analysis.Sphere.ResultId:
                analysis.Cylinder.ResultId;
            string prefix=analysis.Kind==CT3DAnalysisKind.MeshPlane?"plane":
                analysis.Kind==CT3DAnalysisKind.SurfaceSphere?"sphere":"cylinder";
            Guid measurement=scene.Add(MVI3DSceneKind.Measurement,
                "ct3d:"+prefix+":"+feature.ToString("N"),
                analysis.Source.Revision,analysis.Source.Signature);
            if(old!=null)
            {
                foreach(MVI3DSceneItem item in scene.Snapshot())
                {
                    MVI3DSceneItem style=old.FirstOrDefault(x=>x.Kind==item.Kind);
                    if(style==null)continue;
                    scene.SetOrganization(item.Id,style.LayerKey,style.GroupKey);
                    scene.SetOpacity(item.Id,style.Opacity);
                    scene.SetVisibility(item.Id,style.Visible);
                }
            }
            scene.Select(measurement);
        }

        private void OnVolumeStateChanged(object sender,System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName=="SliceX"||e.PropertyName=="SliceY"||e.PropertyName=="SliceZ")
                RefreshSlices();
        }
        private void OnSceneChanged(object sender,EventArgs e)
        {
            if(Dispatcher.CheckAccess())RefreshProfessionalOverlay();
            else Dispatcher.BeginInvoke(new Action(RefreshProfessionalOverlay));
        }
        private void RefreshSlices()
        {
            if(_volume==null||_state==null||_updating)return;
            _updating=true;
            try
            {
                FIMImageBuffer xy=FVOSliceImageBuilder.Build(_volume,_state.SliceX,
                    _state.SliceY,_state.SliceZ,FVOSlicePlane.XY,false,0,255);
                FIMImageBuffer xz=FVOSliceImageBuilder.Build(_volume,_state.SliceX,
                    _state.SliceY,_state.SliceZ,FVOSlicePlane.XZ,false,0,255);
                FIMImageBuffer yz=FVOSliceImageBuilder.Build(_volume,_state.SliceX,
                    _state.SliceY,_state.SliceZ,FVOSlicePlane.YZ,false,0,255);
                _xy.UpdateOriginalFrame(xy);_xz.UpdateOriginalFrame(xz);_yz.UpdateOriginalFrame(yz);
                _scene.SetSlices(xy,xz,yz,_state.SliceX,_state.SliceY,_state.SliceZ,
                    _volume.OriginalWidth,_volume.OriginalHeight,_volume.OriginalDepth,
                    _volume.SpacingX,_volume.SpacingY,_volume.SpacingZ);
                _workbench.RefreshNavigations();
            }
            finally{_updating=false;}
        }
        private void RefreshProfessionalOverlay()
        {
            if(_volume==null||_state==null||CurrentAnalysis==null)return;
            CT3DAnalysisCase analysis=CurrentAnalysis;
            bool shapeVisible=_state.Scene3D.Snapshot().Any(item=>
                item.Visible&&(item.Kind==MVI3DSceneKind.Mesh||
                    item.Kind==MVI3DSceneKind.Surface));
            if(!shapeVisible)
            {
                _scene.ClearSurfaceMesh();_scene.ClearSurfacePoints();
                return;
            }
            if(analysis.Kind==CT3DAnalysisKind.MeshPlane)
            {
                _scene.ClearSurfacePoints();
                var display=MPWVolumeSurfaceProjection.ProjectMeshPreviewLod(
                    analysis.Mesh.Mesh,_volume.SpatialFrame,_volume.OriginalWidth,
                    _volume.OriginalHeight,_volume.OriginalDepth);
                _scene.SetSurfaceMesh(display);
            }
            else
            {
                _scene.ClearSurfaceMesh();
                _scene.SetSurfacePoints(MPWVolumeSurfaceProjection.Project(
                    analysis.Surface.Points,_volume.SpatialFrame,_volume.OriginalWidth,
                    _volume.OriginalHeight,_volume.OriginalDepth,8000));
            }
        }
        private void AnalyzeSelectedRoi()
        {
            if(_current==null||_state==null||_state.SelectedSliceRectangle==null)
                throw new InvalidOperationException("请先打开MeshPlane Case并在二维切片Shift拖选物理ROI。");
            if(_current.Kind!=CT3DAnalysisKind.MeshPlane)
                throw new InvalidOperationException("此ROI工具当前只适用于已验证的Mesh/Plane案例。");
            double minimum,maximum;
            if(!double.TryParse(_minimum.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out minimum)||
                !double.TryParse(_maximum.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out maximum))
                throw new ArgumentException("请输入有效的法向毫米上下限。");
            var result=_current.RecomputeWithSliceRectangle(_root,
                _state.SelectedSliceRectangle,minimum,maximum,CancellationToken.None);
            _pending=result;
            BuildSceneReferences(result,true);
            RefreshProfessionalOverlay();
            _status.Text=String.Format(CultureInfo.InvariantCulture,
                "待保存：平面{0}点、RMS={1:0.######} mm；未作工业不确定度认证。",
                result.Plane.SampleCount,result.Plane.RmsResidualMm);
        }
        private void SaveResult()
        {
            if(_pending==null||_engineering==null||_owners==null||_caseIndex<0)
                throw new InvalidOperationException("当前没有待保存的专业编辑结果。");
            var visual=MVI3DSceneLayoutPersistence.CreateAspect(_state.Scene3D,CancellationToken.None);
            CT3DAnalysisPersistence.Commit(_engineering,_owners[_caseIndex],_engineering.Revision,
                _root,_pending,visual,CancellationToken.None);
            _engineering.SaveAs(_projectPath);
            _current=_pending;_pending=null;
            _status.Text="专业Feature与Scene已在一次MEM事务中保存。";
        }
        private void DisposeCurrent()
        {
            if(_state!=null)
            {
                _state.PropertyChanged-=OnVolumeStateChanged;
                _state.Scene3D.Changed-=OnSceneChanged;
            }
            _workbench.Bind(null,null);_objects.Bind(null);_scene.Clear();
            if(_volume!=null){_volume.Dispose();_volume=null;}
            _state=null;_current=null;_pending=null;
            if(_engineering!=null){_engineering.Close(true);_engineering=null;}
            _owners=null;_projectPath=null;_root=null;_caseIndex=-1;
        }
        public void Dispose()
        {
            if(_disposed)return;
            _disposed=true;DisposeCurrent();
        }
    }
}
