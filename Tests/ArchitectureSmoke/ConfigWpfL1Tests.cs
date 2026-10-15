using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.Automation;
using CxVisual.Professional.Vision;
using CxVisual.Professional.CT3D;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>Windows STA真实创建WPF编辑窗口并模拟保存点击；不会连接工业设备。</summary>
    internal static class ConfigWpfL1Tests
    {
        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "CxVisual-ConfigWpf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Exception failure = null;
            Thread thread = new Thread(() =>
            {
                try { Exercise(root); }
                catch (Exception ex) { failure = ex; }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(32)))
                throw new TimeoutException("Config WPF STA smoke timed out; UI must not deadlock");
            try
            {
                if (failure != null) throw new InvalidOperationException("Config WPF interaction failed", failure);
                Console.WriteLine("PASS ConfigL1 WPF: auto-generated Automation/Vision/CT3D editors, reject invalid input, save and reload valid input");
            }
            finally { Directory.Delete(root, true); }
        }

        private static void Exercise(string root)
        {
            MTCConfigManager m = new MTCConfigManager(root);
            m.Register<CxAutomationDeviceOptions>("Automation.Sample", validator: CxAutomationDeviceOptions.Validate);
            m.Register<CxVisionInspectionOptions>("Vision.Sample", validator: CxVisionInspectionOptions.Validate);
            m.Register<CxCtReconstructionOptions>("CT3D.Sample", validator: CxCtReconstructionOptions.Validate);
            m.Initialize();

            Window automation = MPWConfigEditorDialog.CreateWindow<CxAutomationDeviceOptions>(null, m, "Automation.Sample");
            Check(FindTagged<TextBox>(automation, "ControllerHost") != null &&
                  FindTagged<TextBox>(automation, "ControllerPort") != null &&
                  FindTagged<CheckBox>(automation, "AutoReconnect") != null, "Automation UI generation failed");
            automation.Close();

            Window ct = MPWConfigEditorDialog.CreateWindow<CxCtReconstructionOptions>(null, m, "CT3D.Sample");
            Check(FindTagged<ComboBox>(ct, "ReconstructionMode") != null &&
                  FindTagged<TextBox>(ct, "FodMm") != null &&
                  FindTagged<TextBox>(ct, "FddMm") != null, "CT3D UI generation failed");
            ct.Close();

            Window bad = MPWConfigEditorDialog.CreateWindow<CxVisionInspectionOptions>(null, m, "Vision.Sample");
            bool badAccepted = Modal(bad, w =>
            {
                ComboBox depth = FindTagged<ComboBox>(w, "PixelDepth");
                TextBox threshold = FindTagged<TextBox>(w, "GrayThreshold");
                Check(depth != null && threshold != null, "Vision UI fields missing");
                depth.SelectedItem = CxVisionPixelDepth.Bits8;
                threshold.Text = "256";
                Button save = FindNamed<Button>(w, "ConfigL1_Save");
                Check(save != null, "Save button missing");
                save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, save));
                TextBlock error = FindNamed<TextBlock>(w, "ConfigL1_Error");
                Check(error != null && !string.IsNullOrWhiteSpace(error.Text),
                    "Invalid 8-bit value did not display validation error");
                w.Close();
            });
            Check(!badAccepted && m.GetRevision("Vision.Sample") == 0, "Invalid WPF edit changed valid version");

            Window good = MPWConfigEditorDialog.CreateWindow<CxVisionInspectionOptions>(null, m, "Vision.Sample");
            bool accepted = Modal(good, w =>
            {
                FindTagged<ComboBox>(w, "PixelDepth").SelectedItem = CxVisionPixelDepth.Bits8;
                FindTagged<TextBox>(w, "GrayThreshold").Text = "200";
                FindTagged<CheckBox>(w, "UseAi").IsChecked = true;
                FindTagged<TextBox>(w, "AiModelName").Text = "sample-model";
                Button save = FindNamed<Button>(w, "ConfigL1_Save");
                save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, save));
            });
            Check(accepted, "Valid WPF draft was not saved");
            MTCConfigManager other = new MTCConfigManager(root);
            other.Register<CxVisionInspectionOptions>("Vision.Sample", validator: CxVisionInspectionOptions.Validate);
            other.Initialize();
            CxVisionInspectionOptions v = other.Get<CxVisionInspectionOptions>("Vision.Sample");
            Check(v.PixelDepth == CxVisionPixelDepth.Bits8 && v.GrayThreshold == 200 &&
                  v.UseAi && v.AiModelName == "sample-model", "WPF saved values failed to roundtrip");
        }

        private static bool Modal(Window window, Action<Window> exercise)
        {
            Exception failure = null;
            bool expired = false;
            DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher)
            { Interval = TimeSpan.FromSeconds(11) };
            timer.Tick += (sender, args) =>
            {
                expired = true;
                timer.Stop();
                window.Close();
            };
            window.Loaded += (sender, args) =>
                window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                    new Action(() =>
                    {
                        try { exercise(window); }
                        catch (Exception ex) { failure = ex; window.Close(); }
                    }));
            timer.Start();
            bool accepted = window.ShowDialog() == true;
            timer.Stop();
            if (expired) throw new TimeoutException("WPF dialog could not be operated within 11 seconds");
            if (failure != null) throw failure;
            return accepted;
        }

        private static T FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement
        { return Find<T>(root, name, false); }
        private static T FindTagged<T>(DependencyObject root, string name) where T : FrameworkElement
        { return Find<T>(root, name, true); }
        private static T Find<T>(DependencyObject root, string key, bool tag) where T : FrameworkElement
        {
            if (root == null) return null;
            T value = root as T;
            if (value != null && (tag ? key.Equals(value.Tag as string) : key.Equals(value.Name)))
                return value;
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                DependencyObject element = child as DependencyObject;
                if (element == null) continue;
                T found = Find<T>(element, key, tag);
                if (found != null) return found;
            }
            return null;
        }
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    }
}
