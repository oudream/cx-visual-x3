using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// Desktop UI Automation running on a separate thread against the real TestCenter HWND.
    /// Unlike --ui-smoke this performs ValuePattern, SelectionItemPattern and InvokePattern.
    /// </summary>
    public static class TCTWpfAutomationHarness
    {
        public static async Task RunAsync(TCTMainWindow window)
        {
            if (window == null) throw new ArgumentNullException("window");
            window.Show();
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout());
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) throw new InvalidOperationException("TestCenter has no HWND");
            SaveScreenshot(window, "TestCenter-01-Workspace.png");
            await Task.Run(() => Interact(hwnd, window));
            SaveScreenshot(window, "TestCenterUIA.png");
            SaveScreenshot(window, "TestCenter-02-Geometry.png");
            await Task.Run(() => InteractObject(hwnd, window));
            SaveScreenshot(window, "TestCenter-03-Object.png");
            window.MenuThemeDark.RaiseEvent(new RoutedEventArgs(
                System.Windows.Controls.MenuItem.ClickEvent, window.MenuThemeDark));
            SaveScreenshot(window, "TestCenter-04-Dark.png");
        }
        private static AutomationElement Find(AutomationElement root, string id)
        {
            AutomationElement found = null;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                found = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, id));
                if (found != null) break;
                Thread.Sleep(150);
            }
            if (found == null) throw new InvalidOperationException("UIA control not found: " + id);
            return found;
        }
        private static void Interact(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            if (root == null) throw new InvalidOperationException("UIA HWND not available");
            AutomationElement search = Find(root, "TCT.Explorer.Search");
            ValuePattern input = search.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (input == null || input.Current.IsReadOnly)
                throw new InvalidOperationException("Explorer does not expose editable ValuePattern");
            input.SetValue("TCT.M2.GEOMETRY.001");
            AutomationElement treeItem = Find(root, "TCT.Case.TCT.M2.GEOMETRY.001");
            SelectionItemPattern selector = treeItem.GetCurrentPattern(SelectionItemPattern.Pattern)
                as SelectionItemPattern;
            if (selector == null) throw new InvalidOperationException("Case selection pattern missing");
            selector.Select();
            AutomationElement context = Find(root, "TCT.Context.Geometry");
            InvokePattern modeAction = context.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (modeAction == null)
                throw new InvalidOperationException("Dynamic Geometry Ribbon does not expose InvokePattern");
            modeAction.Invoke();
            AutomationElement source = Find(root, "TCT.Context.Source");
            if (source.GetCurrentPattern(InvokePattern.Pattern) == null)
                throw new InvalidOperationException("Contextual source navigation missing");
            AutomationElement run = Find(root, "TCT.RunSelected");
            InvokePattern invoke = run.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (invoke == null) throw new InvalidOperationException("Run Selected invoke pattern missing");
            invoke.Invoke();

            bool observed = false;
            for (int attempt = 0; attempt < 240; attempt++)
            {
                bool result = false;
                window.Dispatcher.Invoke(new Action(() => {
                    result = window.Results.ResultCount >= 1 &&
                        window.DocumentHost.ViewerCount >= 1 &&
                        window.MenuRunSelected.IsEnabled &&
                        window.Inspector.SelectedObservationTitle == null;
                }));
                if (result) { observed = true; break; }
                Thread.Sleep(200);
            }
            if (!observed)
                throw new TimeoutException("UIA invocation did not lead to verified result and Geometry viewer");
            bool proof = false;
            window.Dispatcher.Invoke(new Action(() => {
                var tabs = window.DocumentHost.Content as System.Windows.Controls.TabControl;
                if (tabs == null) return;
                TCTGeometryTestView geometry = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                    .Select(x => x.Content).OfType<TCTGeometryTestView>().FirstOrDefault();
                proof = geometry != null && geometry.VerifiedFigureCount == 4 &&
                    geometry.EvidenceProblem == null;
            }));
            if (!proof) throw new InvalidOperationException("UIA ran test but did not show verified geometry");
        }
        private static void InteractObject(IntPtr hwnd, TCTMainWindow window)
        {
            AutomationElement root = AutomationElement.FromHandle(hwnd);
            AutomationElement search = Find(root, "TCT.Explorer.Search");
            ValuePattern input = search.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
            if (input == null) throw new InvalidOperationException("Object search has no ValuePattern");
            input.SetValue("M-OBJ-007");
            AutomationElement item = Find(root, "TCT.Case.M-OBJ-007");
            SelectionItemPattern select = item.GetCurrentPattern(SelectionItemPattern.Pattern)
                as SelectionItemPattern;
            if (select == null) throw new InvalidOperationException("Symbol case has no SelectionItemPattern");
            select.Select();
            AutomationElement mode = Find(root, "TCT.Context.Object");
            InvokePattern context = mode.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (context == null) throw new InvalidOperationException("Object Ribbon command not accessible");
            context.Invoke();
            AutomationElement run = Find(root, "TCT.RunSelected");
            InvokePattern invoke = run.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            if (invoke == null) throw new InvalidOperationException("TestCenter run command not accessible");
            invoke.Invoke();
            for (int attempt = 0; attempt < 240; attempt++)
            {
                bool ready = false;
                window.Dispatcher.Invoke(new Action(() =>
                {
                    var tabs = window.DocumentHost.Content as System.Windows.Controls.TabControl;
                    if (tabs == null || !window.MenuRunSelected.IsEnabled) return;
                    TCTGeometryTestView geometry = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(tab => tab.Content).OfType<TCTGeometryTestView>().FirstOrDefault();
                    TCTObjectTestView objects = tabs.Items.OfType<System.Windows.Controls.TabItem>()
                        .Select(tab => tab.Content).OfType<TCTObjectTestView>().FirstOrDefault();
                    ready = geometry != null && objects != null &&
                        geometry.VerifiedFigureCount == 4 && geometry.EvidenceProblem == null &&
                        objects.VerifiedReferenceCount == 2 && objects.RelationDiagram != null &&
                        objects.RelationDiagram.VerifiedReferenceLines == 2 &&
                        objects.EvidenceProblem == null;
                }));
                if (ready) return;
                Thread.Sleep(200);
            }
            throw new TimeoutException("UIA executed Symbol case but did not display verified Geometry/Object evidence");
        }

        private static void SaveScreenshot(TCTMainWindow window, string name)
        {
            window.UpdateLayout();
            int width = (int)Math.Ceiling(window.ActualWidth);
            int height = (int)Math.Ceiling(window.ActualHeight);
            if (width < 500 || height < 350 || width > 4096 || height > 4096)
                throw new InvalidOperationException("UIA screenshot viewport invalid");
            RenderTargetBitmap pixels = new RenderTargetBitmap(width, height, 96, 96,
                PixelFormats.Pbgra32);
            pixels.Render(window);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(pixels));
            string path = Path.Combine(Environment.CurrentDirectory, "Output", "Debug", "x64",
                name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
                encoder.Save(stream);
        }
    }
}
