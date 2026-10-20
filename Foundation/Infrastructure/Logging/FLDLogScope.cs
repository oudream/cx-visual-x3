using System;
using System.Runtime.CompilerServices;

namespace CxVisual.Foundation.Infrastructure.Logging
{
    /// <summary>
    /// F16模块/任务日志上下文值，只有ModuleId和CorrelationId；
    /// 无独立线程、文件句柄或全局可变会话。
    /// </summary>
    public sealed class FLDLogScope
    {
        public FLDLogScope(string moduleId, string correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("Module ID required.", "moduleId");
            ModuleId = moduleId.Trim();
            CorrelationId = correlationId ?? string.Empty;
        }

        public string ModuleId { get; private set; }
        public string CorrelationId { get; private set; }

        /// <summary>合并关联标识，创建新值，不更改原Scope。</summary>
        public FLDLogScope Merge(string correlationId)
        {
            return new FLDLogScope(ModuleId, correlationId);
        }

        public bool Log(FLDLogLevel level, string message, Exception exception = null,
            [CallerMemberName] string member = "")
        {
            return FLDLogHelper.Log(level, message, module: ModuleId,
                correlationId: CorrelationId, exception: exception, member: member);
        }

        public bool Debug(string message, [CallerMemberName] string member = "")
        {
            return FLDLogHelper.Log(FLDLogLevel.Debug, message, module: ModuleId,
                correlationId: CorrelationId, member: member);
        }

        public bool Info(string message, [CallerMemberName] string member = "")
        {
            return FLDLogHelper.Log(FLDLogLevel.Information, message, module: ModuleId,
                correlationId: CorrelationId, member: member);
        }

        public bool Information(string message, [CallerMemberName] string member = "")
        {
            return Info(message, member);
        }

        public bool Warning(string message, [CallerMemberName] string member = "")
        {
            return FLDLogHelper.Log(FLDLogLevel.Warning, message, module: ModuleId,
                correlationId: CorrelationId, member: member);
        }

        public bool Error(string message, Exception exception = null,
            [CallerMemberName] string member = "")
        {
            return FLDLogHelper.Log(FLDLogLevel.Error, message, module: ModuleId,
                correlationId: CorrelationId, exception: exception, member: member);
        }
    }
}
