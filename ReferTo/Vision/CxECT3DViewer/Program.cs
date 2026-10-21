using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommonInterfaces;

namespace CxECT3DViewer
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            LogHelper.Run("CxECT3DViewer");
            try
            {
                Application.Run(new MainForm());
            }
            finally
            {
                LogHelper.Stop();
            }
        }
    }
}
