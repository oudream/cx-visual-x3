using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CxVisual.TestCenter.Models
{
    public sealed class TCTTestSourceLocation
    {
        public TCTTestSourceLocation(string file, int line, string method)
        {
            File = file; Line = line; Method = method ?? "";
        }
        public string File { get; private set; }
        public int Line { get; private set; }
        public string Method { get; private set; }
        public string Display { get { return Path.GetFileName(File) + ":" + Line; } }
    }

    public static class TCTTestSourceLocator
    {
        // CLR/NUnit stack frames with embedded PDB source positions only; never guess missing paths.
        private static readonly Regex Pattern = new Regex(
            @"\bat\s+([^\r\n]+?)\s+in\s+(.+?):line\s+(\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public static TCTTestSourceLocation TryParse(string stack)
        {
            if (string.IsNullOrWhiteSpace(stack)) return null;
            foreach (Match match in Pattern.Matches(stack))
            {
                int line;
                if (!int.TryParse(match.Groups[3].Value, out line) || line < 1) continue;
                string file = match.Groups[2].Value.Trim();
                if (!Path.IsPathRooted(file)) continue;
                return new TCTTestSourceLocation(file, line, match.Groups[1].Value.Trim());
            }
            return null;
        }
    }
}
