using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Execution
{
    // Worker -> TCT parent append-only event journal. A truncated final line is not an event.
    // No process network listener, and every message is bounded before writing.
    public sealed class TCTTestEventJournal
    {
        private readonly string _file;
        private readonly object _gate = new object();
        public TCTTestEventJournal(string file)
        {
            _file = Path.GetFullPath(file ?? throw new ArgumentNullException("file"));
            if (!Directory.Exists(Path.GetDirectoryName(_file)))
                throw new DirectoryNotFoundException("Event output directory missing");
        }
        public void Write(TCTTestEvent value)
        {
            if (value == null) throw new ArgumentNullException("value");
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Message));
            if (encoded.Length > 256 * 1024)
                throw new InvalidDataException("Test event exceeds bounded journal record");
            string line = value.Sequence.ToString(CultureInfo.InvariantCulture) + "|" +
                (int)value.Kind + "|" + value.TimestampUtc.Ticks.ToString(CultureInfo.InvariantCulture) +
                "|" + encoded + "\n";
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            lock (_gate)
            {
                using (FileStream fs = new FileStream(_file, FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
            }
        }
        public IList<TCTTestEvent> ReadNew(string attemptId, ref int alreadyRead)
        {
            List<TCTTestEvent> output = new List<TCTTestEvent>();
            if (!File.Exists(_file)) return output;
            string text;
            using (FileStream fs = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8, true))
                text = sr.ReadToEnd();
            string[] lines = text.Split('\n');
            int finished = lines.Length - 1;
            if (alreadyRead > finished) throw new InvalidDataException("Event journal shrank unexpectedly");
            for (int i = alreadyRead; i < finished; i++)
            {
                string[] parts = lines[i].TrimEnd('\r').Split(new[] {'|'}, 4);
                if (parts.Length != 4) throw new InvalidDataException("Malformed event record");
                long seq = long.Parse(parts[0], CultureInfo.InvariantCulture);
                int kind = int.Parse(parts[1], CultureInfo.InvariantCulture);
                long ticks = long.Parse(parts[2], CultureInfo.InvariantCulture);
                if (seq != i + 1 || !Enum.IsDefined(typeof(TCTTestEventKind), kind))
                    throw new InvalidDataException("Event sequence/kind validation failed");
                string msg = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
                output.Add(new TCTTestEvent(seq, attemptId, (TCTTestEventKind)kind, msg,
                    new DateTime(ticks, DateTimeKind.Utc)));
            }
            alreadyRead = finished;
            return output;
        }
    }
}
