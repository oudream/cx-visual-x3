using System;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>
    /// 重要事件专用文件Sink，与普通日志文件隔离。采用同步持久写入；
    /// 是否属于重要事件由调用方显式标记，不依据文件名或类名推断。
    /// </summary>
    public sealed class CxImportantEventFileSink : ICxLogSink, IDisposable
    {
        private readonly CxFileLogSink _file;

        public CxImportantEventFileSink(string path)
        {
            _file = new CxFileLogSink(path, true);
        }

        public void Write(CxLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            if (record.IsImportant) _file.Write(record);
        }

        public void Dispose() { _file.Dispose(); }
    }
}
