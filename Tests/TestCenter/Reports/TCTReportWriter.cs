using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.Reports
{
    public static class TCTReportWriter
    {
        public static void WriteText(string path, TCTTestRunSummary summary)
        {
            WriteAtomic(path, string.Join(Environment.NewLine, new[] {
                "CxVisual.TestCenter " + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                "Total=" + summary.Total + " Passed=" + summary.Passed +
                " Failed=" + summary.Failed + " Cancelled=" + summary.Cancelled +
                " Unsupported=" + summary.Unsupported
            }.Concat(summary.Results.SelectMany(r =>
                new[] { r.Status + " | " + r.Descriptor.Id + " | " + r.Descriptor.Name +
                        " | " + r.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + "ms",
                        r.Error.Length > 0 ? r.Error : "" }.Concat(
                r.Assertions.Select(a => "  " + (a.Passed ? "PASS" : "FAIL") + " " + a.Name +
                        " expected=" + a.Expected + " actual=" + a.Actual))))) + Environment.NewLine);
        }
        public static void WriteJson(string path, TCTTestRunSummary summary)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            object data = new {
                schemaVersion = 1, generatedUtc = DateTime.UtcNow.ToString("o"),
                total = summary.Total, passed = summary.Passed, failed = summary.Failed,
                cancelled = summary.Cancelled, unsupported = summary.Unsupported,
                success = summary.Success,
                cases = summary.Results.Select(r => new {
                    id = r.Descriptor.Id, name = r.Descriptor.Name,
                    aspect = r.Descriptor.Aspect.ToString(), group = r.Descriptor.Group,
                    capabilityAreaId = r.Descriptor.CapabilityAreaId,
                    capabilityId = r.Descriptor.CapabilityId, objectId = r.Descriptor.ObjectId,
                    status = r.Status.ToString(), milliseconds = r.Elapsed.TotalMilliseconds,
                    error = r.Error, assertions = r.Assertions.Select(a => new {
                        a.Name, a.Expected, a.Actual, a.Passed
                    }).ToArray(),
                    observations = r.Observations.Select(o => new {
                        o.Kind, o.Title, o.Details, o.Origin
                    }).ToArray()
                }).ToArray()
            };
            WriteAtomic(path, serializer.Serialize(data));
        }
        public static void WriteJUnit(string path, TCTTestRunSummary summary)
        {
            XElement suite = new XElement("testsuite",
                new XAttribute("name", "CxVisual.TestCenter"),
                new XAttribute("tests", summary.Total),
                new XAttribute("failures", summary.Results.Count(r => r.Status == TCTTestStatus.Failed)),
                new XAttribute("errors", summary.Results.Count(r => r.Status == TCTTestStatus.Error || r.Status == TCTTestStatus.TimedOut)),
                new XAttribute("skipped", summary.Results.Count(r => r.Status == TCTTestStatus.Skipped ||
                    r.Status == TCTTestStatus.Unsupported || r.Status == TCTTestStatus.Inconclusive ||
                    r.Status == TCTTestStatus.Cancelled)),
                summary.Results.Select(r => {
                    XElement test = new XElement("testcase",
                        new XAttribute("classname", r.Descriptor.Aspect + "." + r.Descriptor.Group),
                        new XAttribute("name", r.Descriptor.Id),
                        new XAttribute("time", r.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)));
                    if (r.Status == TCTTestStatus.Failed)
                        test.Add(new XElement("failure", new XAttribute("message", "Assertion failed"), r.Error));
                    else if (r.Status == TCTTestStatus.Error || r.Status == TCTTestStatus.TimedOut)
                        test.Add(new XElement("error", new XAttribute("message", r.Status), r.Error));
                    else if (r.Status != TCTTestStatus.Passed)
                        test.Add(new XElement("skipped", new XAttribute("message", r.Status)));
                    return test;
                }));
            WriteAtomic(path, new XDocument(new XDeclaration("1.0", "utf-8", "yes"), suite).ToString());
        }
        private static void WriteAtomic(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Report path required", "path");
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, content, new UTF8Encoding(false));
                if (File.Exists(full)) File.Delete(full);
                File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
