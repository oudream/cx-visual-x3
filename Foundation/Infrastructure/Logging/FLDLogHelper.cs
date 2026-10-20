using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace CxVisual.Foundation.Infrastructure.Logging
{
    /// <summary>F16实时日志通知，回调运行于日志分发线程，WPF订阅者必须切换到Dispatcher。</summary>
    public delegate void FLDLogEventHandler(FLDLogRecord record);

    /// <summary>
    /// 应用级静态便捷入口。仅保存一个由应用明确初始化或附加的FLDLogService；
    /// 多Host与自动测试可直接使用FLDLogService，互不影响。
    /// </summary>
    public static class FLDLogHelper
    {
        private static readonly object Gate = new object();
        private static readonly List<KeyValuePair<string, string>> ModuleRules
            = new List<KeyValuePair<string, string>>();
        private static FLDLogService _service;
        private static bool _ownsService;
        private static Exception _lastCallbackError;

        /// <summary>实时日志：记录接受入队后由后台线程通知；不代表磁盘已成功写入。</summary>
        public static event FLDLogEventHandler LogEvent;

        public static Exception LastCallbackError
        {
            get { lock (Gate) { return _lastCallbackError; } }
        }

        public static bool IsInitialized
        {
            get { lock (Gate) { return _service != null; } }
        }

        /// <summary>由应用程序启动时调用一次，路径由宿主确定，不写死EXE目录。</summary>
        public static void Initialize(string logRoot)
        {
            FLDLogService created = new FLDLogService(logRoot);
            try { Install(created, true); }
            catch { created.Dispose(); throw; }
        }

        /// <summary>挂接应用已有实例；该实例仍由原所有者Dispose。</summary>
        public static void Attach(FLDLogService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            Install(service, false);
        }

        private static void Install(FLDLogService service, bool owns)
        {
            lock (Gate)
            {
                if (_service != null)
                    throw new InvalidOperationException("FLDLogHelper is already initialized.");
                _service = service;
                _ownsService = owns;
                _lastCallbackError = null;
                _service.RecordReceived += NotifyRealtime;
            }
        }

        /// <summary>移除静态入口；仅当Initialize创建服务时负责Dispose。</summary>
        public static void Stop()
        {
            FLDLogService service;
            bool owns;
            lock (Gate)
            {
                service = _service;
                if (service == null) return;
                _service = null;
                owns = _ownsService;
                _ownsService = false;
                service.RecordReceived -= NotifyRealtime;
            }
            if (owns) service.Dispose();
        }

        /// <summary>获取日志引擎实例。未初始化时明确抛错，禁止静默丢日志。</summary>
        public static FLDLogService Current
        {
            get
            {
                lock (Gate)
                {
                    if (_service == null)
                        throw new InvalidOperationException(
                            "Call FLDLogHelper.Initialize(logRoot) or Attach(service) on application startup.");
                    return _service;
                }
            }
        }

        /// <summary>注册源码文件名到专业模块ID的映射，显式传module始终优先。</summary>
        public static void RegisterModuleRule(string fileNamePattern, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(fileNamePattern))
                throw new ArgumentException("File pattern required.", "fileNamePattern");
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Module ID required.", "moduleId");
            lock (Gate)
            {
                for (int i = ModuleRules.Count - 1; i >= 0; --i)
                    if (string.Equals(ModuleRules[i].Key, fileNamePattern,
                        StringComparison.OrdinalIgnoreCase)) ModuleRules.RemoveAt(i);
                ModuleRules.Add(new KeyValuePair<string, string>(fileNamePattern, moduleId));
            }
        }

        private static string ResolveModule(string explicitModule, string callerFile)
        {
            if (!string.IsNullOrWhiteSpace(explicitModule)) return explicitModule.Trim();
            string filename = Path.GetFileNameWithoutExtension(callerFile);
            lock (Gate)
            {
                KeyValuePair<string, string> best = default(KeyValuePair<string, string>);
                foreach (KeyValuePair<string, string> rule in ModuleRules)
                    if (!string.IsNullOrEmpty(filename) &&
                        filename.IndexOf(rule.Key, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        rule.Key.Length > (best.Key == null ? 0 : best.Key.Length))
                        best = rule;
                if (best.Key != null) return best.Value;
            }
            return string.IsNullOrWhiteSpace(filename) ? "Application" : filename;
        }

        private static void NotifyRealtime(FLDLogRecord record)
        {
            FLDLogEventHandler handlers = LogEvent;
            if (handlers == null) return;
            foreach (Delegate handler in handlers.GetInvocationList())
                try { ((FLDLogEventHandler)handler)(record); }
                catch (Exception ex) { lock (Gate) { _lastCallbackError = ex; } }
        }

        public static bool Log(FLDLogLevel level, string message, string module = null,
            string dedicatedName = null, bool important = false, string correlationId = null,
            Exception exception = null, [CallerMemberName] string member = "",
            [CallerFilePath] string file = "")
        {
            return Current.Log(level, ResolveModule(module, file), message,
                dedicatedName, member, important, correlationId, exception);
        }

        public static bool Info(string message, string module = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "")
        {
            return Log(FLDLogLevel.Information, message, module, member: member, file: file);
        }

        public static bool Information(string message, string module = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "")
        {
            return Log(FLDLogLevel.Information, message, module, member: member, file: file);
        }

        public static bool Debug(string message, string module = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "")
        {
            return Log(FLDLogLevel.Debug, message, module, member: member, file: file);
        }

        public static bool Warning(string message, string module = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "")
        {
            return Log(FLDLogLevel.Warning, message, module, member: member, file: file);
        }

        public static bool Error(string message, Exception exception = null, string module = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "")
        {
            return Log(FLDLogLevel.Error, message, module, exception: exception, member: member, file: file);
        }

        public static bool Critical(string message, Exception exception = null, string module = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "")
        {
            return Log(FLDLogLevel.Critical, message, module, exception: exception,
                member: member, file: file);
        }

        public static bool Important(string message, string module = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "")
        {
            return Log(FLDLogLevel.Information, message, module, important: true,
                member: member, file: file);
        }

        /// <summary>保留专业模块上下文的便捷入口。</summary>
        public static FLDLogScope ForModule(string moduleId)
        {
            return new FLDLogScope(moduleId);
        }

        /// <summary>已有业务实例或通道专有文件日志入口。</summary>
        public static FLDDedicatedLogger ForDedicated(string moduleId, string dedicatedName)
        {
            return Current.ForDedicated(moduleId, dedicatedName);
        }

        /// <summary>与旧LogHelper.SetLogLevel对应，运行时调整最低日志等级。</summary>
        public static void SetLogLevel(FLDLogLevel minimumLevel)
        {
            Current.MinimumLevel = minimumLevel;
        }

        public static bool Fatal(string message, Exception exception = null, string module = null,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "")
        {
            return Critical(message, exception, module, member, file);
        }

        public static FLDLogRecord[] Snapshot() { return Current.Snapshot(); }
        public static void Flush(int millisecondsTimeout = 10000)
        {
            Current.Flush(millisecondsTimeout);
        }
    }
}
