using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CxVisual.Foundation.Infrastructure.Logging;

namespace CxVisual.Foundation.Infrastructure.Logging.Sinks
{
    public interface IFLDLogSink
    {
        void Write(FLDLogRecord record);
    }
    /// <summary>单实例分类日志Sink，无全局单例；外部持有生命周期。</summary>
}
