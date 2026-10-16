using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Presentation.Wpf;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>
    /// Real Windows STA construction and interaction smoke; does not claim a human
    /// has checked pixel layout, keyboard navigation or hardware configuration.
    /// </summary>
    internal static class ConfigWpfTests
    {
        public static int Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxConfig-Wpf-" + Guid.NewGuid().ToString("N"));
            Exception failure = null;
            Thread sta = new Thread(() =>
            {
                try
                {
                    MHSConfigService service = new MHSConfigService(root);
                    MHSConfigSection<V13ConfigSample> section =
                        service.ForMachine("WPF").Open<V13ConfigSample>();
                    using (var control = new MPWConfigEditorControl<V13ConfigSample>(section))
                    {
                        if (control.VisibleEditorCount < 4 || control.Content == null)
                            throw new InvalidOperationException("CONFIG_WPF_SCHEMA_RENDER_FAILED");
                        Action<string> change = null;
                        control.RegisterEditor("ExposureMs", (value, update) =>
                        {
                            change = update;
                            return new TextBox { Text = value };
                        });
                        if (change == null) throw new InvalidOperationException("CONFIG_WPF_EDITOR_EXTENSION_MISSING");
                        change("145");
                        if (control.Draft.ExposureMs != 145 ||
                            section.GetLatestSaved().ExposureMs != 80)
                            throw new InvalidOperationException("CONFIG_WPF_EDITOR_TRANSACTION_BYPASS");
                    }
                    if (section.GetLatestSaved().ExposureMs != 80)
                        throw new InvalidOperationException("CONFIG_WPF_DISCARDED_DRAFT_WROTE_TO_DISK");

                    var layers = new[] {
                        new MVBConfigLayerSpec("Template", "template", "WPF-Base", 10),
                        new MVBConfigLayerSpec("Machine", "machine", "WPF", 20)
                    };
                    var atomic = new MVBConfigAtomicLayerProfile<V13ConfigSample>(
                        service, "WPF-Atomic", layers);
                    var atomicView = new MPWConfigLayerOverview<V13ConfigSample>(atomic);
                    if (atomicView.Content == null || atomicView.DisplayedSourceCount != 2)
                        throw new InvalidOperationException("CONFIG_WPF_ATOMIC_SOURCE_RENDER");
                    var independent = new MVBConfigLayeredProfile<V13ConfigSample>(
                        service, "WPF-Independent", layers);
                    var layerView = new MPWConfigLayerOverview<V13ConfigSample>(independent);
                    if (layerView.Content == null || layerView.DisplayedSourceCount != 2)
                        throw new InvalidOperationException("CONFIG_WPF_INDEPENDENT_SOURCE_RENDER");
                }
                catch (Exception ex) { failure = ex; }
            });
            sta.SetApartmentState(ApartmentState.STA);
            sta.Start();
            if (!sta.Join(TimeSpan.FromSeconds(45)))
            {
                Console.Error.WriteLine("FAIL CONFIG_WPF_STA_TIMEOUT");
                return 1;
            }
            try
            {
                if (failure != null) throw failure;
                Console.WriteLine("PASS Config WPF STA editor, custom field and two layer presentation modes");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL Config WPF STA: " + ex);
                return 1;
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
