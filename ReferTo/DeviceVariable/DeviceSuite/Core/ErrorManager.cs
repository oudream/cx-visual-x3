using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxDeviceSuite.Core
{
    public class ErrorInfo
    {
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; }
        public string ModuleName { get; set; }
        public string ProcessName { get; set; }
    }

    public interface IErrorHandler
    {
        void HandleError(ErrorInfo error);
    }


    internal class ErrorManager
    {
    }
    public static class ErrorHandlingCenter
    {
        private static List<IErrorHandler> handlers = new List<IErrorHandler>();

        public static void RegisterHandler(IErrorHandler handler)
        {
            handlers.Add(handler);
        }

        public static void HandleError(ErrorInfo error)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler.HandleError(error);
                }
                catch
                {
                    // 忽略处理器中的错误，或记录到备用日志
                }
            }
        }
    }

}
