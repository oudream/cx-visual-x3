using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CxVisual.Mechanism.Core;
using CxVisual.Professional.HMI;

namespace CxVisual.Studio
{
    /// <summary>
    /// Studio仅消费Runtime.Host正式工程预检，不创建新的设备表、配置仓或PLC驱动。
    /// </summary>
    public partial class MainWindow
    {
        private static string QuoteHmiPreflightPath(string path)
        {
            if(String.IsNullOrWhiteSpace(path)||path.IndexOf('"')>=0||
                path.IndexOfAny(new[]{'\r','\n','\0'})>=0)
                throw new ArgumentException("hmi.preflight.invalid_path");
            int backslashes=0;
            for(int i=path.Length-1;i>=0&&path[i]=='\\';i--)backslashes++;
            return "\"" + path + new string('\\',backslashes) + "\"";
        }

        private static PHMScadaEngineeringInspection RunHmiEngineeringPreflight(
            string executable,string projectFile,string mhsRoot)
        {
            string exe=Path.GetFullPath(executable);
            string project=Path.GetFullPath(projectFile);
            string root=Path.GetFullPath(mhsRoot);
            if(!String.Equals(Path.GetFileName(exe),"CxVisual.Runtime.Host.exe",
                StringComparison.OrdinalIgnoreCase)||!File.Exists(exe))
                throw new FileNotFoundException("请选择真实CxVisual.Runtime.Host.exe",exe);
            if(!File.Exists(project)||!Directory.Exists(root))
                throw new FileNotFoundException("已保存工程或MHS配置目录不存在");
            ProcessStartInfo start=new ProcessStartInfo {
                FileName=exe,WorkingDirectory=Path.GetDirectoryName(exe),
                Arguments="scada-project-check "+
                    QuoteHmiPreflightPath(project)+" "+QuoteHmiPreflightPath(root),
                UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            using(Process process=new Process {StartInfo=start})
            {
                if(!process.Start())
                    throw new InvalidOperationException("hmi.preflight.host_start_failed");
                Task<string> output=process.StandardOutput.ReadToEndAsync();
                Task<string> errors=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(15000))
                {
                    try{process.Kill();}catch(InvalidOperationException){}
                    throw new TimeoutException("Runtime.Host工程预检超过15秒，未确认配置有效");
                }
                Task.WaitAll(output,errors);
                if(process.ExitCode!=0)
                    throw new InvalidOperationException("Runtime.Host工程预检拒绝(exit="+
                        process.ExitCode+"): "+errors.Result.Substring(0,
                            Math.Min(errors.Result.Length,400)));
                return PHMScadaEngineeringInspection.Parse(output.Result.Trim());
            }
        }

        private void OpenStudioScadaPreflightDialog(MEMEngineeringSession original)
        {
            if(!ReferenceEquals(_viewModel.ProjectSession,original))
                throw new InvalidOperationException("HMI工程已切换");
            Window dialog=new Window {
                Title="SCADA工程与点表预检（未连接PLC）",
                Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,
                Width=920,Height=570,MinWidth=740,MinHeight=440
            };
            DockPanel panel=new DockPanel {Margin=new Thickness(12)};
            StackPanel header=new StackPanel();
            header.Children.Add(new TextBlock {
                Text="仅验证已保存MEM与MHS Effective；不启动采集、不验证通信、不授权设备控制。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)
            });
            header.Children.Add(new TextBlock {Text="Runtime.Host可执行程序（确认运行来源）"});
            TextBox exe=new TextBox {
                Name="StudioHmiPreflightExecutable",
                Text=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "CxVisual.Runtime.Host.exe"),Margin=new Thickness(0,2,0,7)
            };
            header.Children.Add(exe);
            header.Children.Add(new TextBlock {Text="当前保存的MEM工程文件"});
            TextBox engineeringPath=new TextBox {
                IsReadOnly=true,Text=original.Location??"(未保存)",
                Margin=new Thickness(0,2,0,7)
            };
            header.Children.Add(engineeringPath);
            header.Children.Add(new TextBlock {Text="与Runtime.Host一致的正式MHS配置根"});
            TextBox root=new TextBox {
                Name="StudioHmiPreflightMhsRoot",
                Text=_studioConfigurationStorageRoot??"",
                Margin=new Thickness(0,2,0,7)
            };
            header.Children.Add(root);
            Button run=new Button {
                Name="StudioHmiPreflightExecute",
                Content="执行正式Host工程预检（只读）",
                MinHeight=32,HorizontalAlignment=HorizontalAlignment.Left,
                Padding=new Thickness(12,3,12,3)
            };
            header.Children.Add(run);
            TextBlock status=new TextBlock {
                Name="StudioHmiPreflightStatus",
                Text="尚未验证；需要先保存MEM工程并让MHS配置正式生效。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)
            };
            header.Children.Add(status);
            DockPanel.SetDock(header,Dock.Top);panel.Children.Add(header);
            ListView table=new ListView {Name="StudioHmiPreflightPoints"};
            GridView columns=new GridView();
            columns.Columns.Add(new GridViewColumn {Header="Scope",Width=130,
                DisplayMemberBinding=new Binding("Scope")});
            columns.Columns.Add(new GridViewColumn {Header="设备",Width=220,
                DisplayMemberBinding=new Binding("Device")});
            columns.Columns.Add(new GridViewColumn {Header="PointId",Width=120,
                DisplayMemberBinding=new Binding("Point")});
            columns.Columns.Add(new GridViewColumn {Header="地址",Width=140,
                DisplayMemberBinding=new Binding("Address")});
            columns.Columns.Add(new GridViewColumn {Header="类型",Width=90,
                DisplayMemberBinding=new Binding("Type")});
            columns.Columns.Add(new GridViewColumn {Header="可写定义≠授权",Width=105,
                DisplayMemberBinding=new Binding("Writable")});
            table.View=columns;panel.Children.Add(table);
            dialog.Content=panel;
            run.Click+=async (sender,args)=>
            {
                run.IsEnabled=false;table.ItemsSource=null;
                int revision=original.Revision;
                string savedFile=original.Location;
                try
                {
                    if(!ReferenceEquals(original,_viewModel.ProjectSession)||
                       original.IsDirty||String.IsNullOrWhiteSpace(savedFile)||
                       !File.Exists(savedFile)||
                       String.IsNullOrWhiteSpace(_studioConfigurationStorageRoot)||
                       !String.Equals(Path.GetFullPath(root.Text.Trim()),
                           _studioConfigurationStorageRoot,StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "请先保存工程、正式生效MHS配置，并使用Studio当前已确认的MHS根");
                    string selectedExe=exe.Text.Trim();
                    string selectedRoot=_studioConfigurationStorageRoot;
                    PHMScadaEngineeringInspection result=await Task.Run(()=>
                        RunHmiEngineeringPreflight(selectedExe,savedFile,selectedRoot));
                    if(!dialog.IsVisible)return;
                    if(!ReferenceEquals(original,_viewModel.ProjectSession)||
                       original.Revision!=revision||original.IsDirty||
                       !String.Equals(savedFile,original.Location,
                           StringComparison.OrdinalIgnoreCase)||
                       !String.Equals(selectedRoot,_studioConfigurationStorageRoot,
                           StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "工程Revision或MHS配置根在预检期间改变，拒绝展示旧结果");
                    foreach(PHMScadaEngineeringDevice item in result.Devices)
                        if(!(original.Find(Guid.Parse(item.DeviceRef)) is IMEMResource))
                            throw new InvalidOperationException(
                                "Host返回了不属于当前MEM工程的DeviceRef");
                    var rows=result.Devices.SelectMany(d=>d.Points.Select(p=>new {
                        Scope=d.ScopeId,Device=d.DeviceRef,Point=p.StablePointId,
                        Address=p.VariableAddress,Type=p.ValueType,
                        Writable=p.WritableDefinition?"是（未授权）":"否"
                    })).Take(1000).ToArray();
                    table.ItemsSource=rows;
                    status.Text="工程配置有效（未验证设备通信） · 部署版本 "+
                        result.DeploymentVersion+" · "+result.Devices.Length+
                        " 设备 / "+result.Devices.Sum(d=>d.Points.Length)+
                        " 点（最多显示1000） · DeviceCommunicationValidated=false";
                }
                catch(Exception error)
                {if(dialog.IsVisible)status.Text="工程预检未通过："+error.Message;}
                finally{run.IsEnabled=true;}
            };
            dialog.ShowDialog();
        }
    }
}
