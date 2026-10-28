using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Presentation.Wpf;
namespace CxVisual.Studio
{
    /// <summary>Read-only MOC projection. The current source is a CPU demonstration resolver.</summary>
    public partial class MainWindow
    {
        private void OnOpenStudioModuleCenter(object sender,RoutedEventArgs args)
        {SafeStudioAction(()=>OpenStudioDocument("doc.modules"));}
        private MPWModuleCenterControl CreateStudioModuleCenterDocument()
        {
            var control=new MPWModuleCenterControl();
            control.HeaderContent=new TextBlock{
                Text="工程："+_viewModel.ProjectSession.Project.Name+
                    "。当前仅展示已有MOC CPU演示Resolver，非真实专业Provider就绪。",
                Margin=new Thickness(4,4,4,6),TextWrapping=TextWrapping.Wrap};
            control.SetCapabilities(CurrentCatalog().All.Select(item=>new MPWModuleCapabilityView(
                item.Definition.Id,item.Definition.Name,item.Definition.Category,
                MFMModuleKind.Simulation,item.Definition.Version,item.RuntimeAvailable,true)),
                "Studio MOC CPU演示目录（非实机授权）");
            return control;
        }
    }
}
