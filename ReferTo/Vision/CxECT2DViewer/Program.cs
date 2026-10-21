using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CxECT2DViewer
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length == 2 && string.Equals(args[0], "--verify-open", StringComparison.OrdinalIgnoreCase))
            {
                RunOpenImageVerification(args[1]);
                return;
            }

            Application.Run(new MainForm());
        }

        /// <summary>
        /// 使用真实窗口和真实 HALCON 图窗验证文件加载；成功返回 0，便于命令行自动检查。
        /// </summary>
        private static void RunOpenImageVerification(string fileName)
        {
            try
            {
                using (var form = new MainForm())
                {
                    form.Show();
                    Application.DoEvents();
                    Console.Error.WriteLine("验收：未加载图像的位置复位");
                    form.VerifyNoImageNavigation();
                    Console.Error.WriteLine("验收：加载图像");
                    form.LoadImageFile(fileName);
                    Application.DoEvents();
                    Console.Error.WriteLine("验收：执行核心功能");
                    form.VerifyImageViewerCoreFunctions(fileName);
                    Console.Error.WriteLine("验收：核心功能完成");
                    Environment.ExitCode = form.HasLoadedImage ? 0 : 1;
                    // HALCON 图窗会创建原生窗口资源；在自动验证结束前显式关闭窗体，确保进程不滞留。
                    form.Close();
                    Application.DoEvents();
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                Environment.ExitCode = 1;
            }
            finally
            {
                Application.ExitThread();
                // 自动验收没有 Application.Run 消息循环；显式结束进程，避免 HALCON 原生窗口残留使 CI 挂起。
                Environment.Exit(Environment.ExitCode);
            }
        }
    }
}
