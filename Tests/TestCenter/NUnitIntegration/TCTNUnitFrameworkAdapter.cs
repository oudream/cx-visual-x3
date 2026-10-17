using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using NUnit.Engine;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Fixtures;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.NUnitIntegration
{
    // The engine is the sole authority for NUnit discovery and outcomes.
    // Existing TCT orchestration only supplies Run/Attempt identity and process isolation.
    public sealed class TCTNUnitFrameworkAdapter : ITCTTestFrameworkAdapter
    {
        private readonly string _assemblyPath;
        public TCTNUnitFrameworkAdapter(string assemblyPath)
        {
            _assemblyPath = Path.GetFullPath(assemblyPath ?? throw new ArgumentNullException("assemblyPath"));
        }
        public string Id { get { return "NUnit.Engine.3.22"; } }

        public IEnumerable<ITCTTestCase> Discover()
        {
            List<ITCTTestCase> cases = new List<ITCTTestCase>();
            string fingerprint = TCTNUnitBridge.Fingerprint(_assemblyPath);
            string stem = Path.GetFileNameWithoutExtension(_assemblyPath);
            bool builtIn = string.Equals(Path.GetFullPath(_assemblyPath),
                Path.GetFullPath(typeof(TCTNUnitFrameworkAdapter).Assembly.Location),
                StringComparison.OrdinalIgnoreCase);
            using (ITestEngine engine = TestEngineActivator.CreateInstance())
            {
                TestPackage package = TCTNUnitBridge.CreatePackage(_assemblyPath);
                using (ITestRunner runner = engine.GetRunner(package))
                {
                    XmlNode tree = runner.Explore(TestFilter.Empty);
                    foreach (XmlNode node in tree.SelectNodes("//test-case"))
                    {
                        string fullname = TCTNUnitBridge.Read(node, "fullname");
                        string display = TCTNUnitBridge.Read(node, "name");
                        if (string.IsNullOrEmpty(fullname))
                            throw new InvalidDataException("NUnit test-case without FullName");
                        cases.Add(new TCTNUnitTestCase(_assemblyPath, fullname, display,
                            fingerprint, builtIn ? null : stem));
                    }
                }
            }
            if (TCTNUnitBridge.Fingerprint(_assemblyPath) != fingerprint)
                throw new InvalidDataException("Assembly changed while NUnit was discovering tests");
            return cases;
        }
    }

    public sealed class TCTNUnitTestCase : ITCTExternalResultTestCase,
        ITCTTestDefinitionFingerprint, ITCTParameterizedCaseIdentity
    {
        private readonly string _assemblyPath;
        private readonly string _fullName;
        private readonly string _fingerprint;
        public TCTNUnitTestCase(string assemblyPath, string fullName, string displayName,
            string fingerprint = null, string externalAssemblyName = null)
        {
            _assemblyPath = Path.GetFullPath(assemblyPath ?? throw new ArgumentNullException("assemblyPath"));
            _fullName = fullName ?? throw new ArgumentNullException("fullName");
            _fingerprint = fingerprint ?? TCTNUnitBridge.Fingerprint(_assemblyPath);
            string prefix = string.IsNullOrWhiteSpace(externalAssemblyName) ?
                "NUNIT." : "NUNIT." + externalAssemblyName + "::";
            CaseId = prefix + fullName;
            int parameterOffset = fullName.IndexOf('(');
            DefinitionId = prefix + (parameterOffset < 0 ? fullName :
                fullName.Substring(0, parameterOffset));
            RowKey = parameterOffset < 0 ? "default" : fullName.Substring(parameterOffset);
            Descriptor = new TCTTestCaseDescriptor(CaseId,
                string.IsNullOrWhiteSpace(displayName) ? fullName : displayName,
                TCTTestAspect.Foundation, "NUnit", "Unmapped", "Unmapped", "NUnit.Framework");
        }
        public string DefinitionFingerprint { get { return _fingerprint; } }
        public string DefinitionId { get; private set; }
        public string CaseId { get; private set; }
        public string RowKey { get; private set; }
        public TCTTestCaseDescriptor Descriptor { get; private set; }
        public void Execute(TCTTestContext context)
        {
            throw new NotSupportedException("NUnit tests must use their native result bridge");
        }

        public TCTTestCaseResult ExecuteExternal(TCTTestAttempt attempt,
            CancellationToken cancellation, TCTTestArtifactStore artifacts)
        {
            Stopwatch watch = Stopwatch.StartNew();
            cancellation.ThrowIfCancellationRequested();
            if (TCTNUnitBridge.Fingerprint(_assemblyPath) != _fingerprint)
                throw new InvalidDataException("NUnit assembly changed after discovery");
            XmlNode result;
            using (ITestEngine engine = TestEngineActivator.CreateInstance())
            using (ITestRunner runner = engine.GetRunner(TCTNUnitBridge.CreatePackage(_assemblyPath)))
            {
                string filterXml = "<filter><test>" + SecurityElement.Escape(_fullName) + "</test></filter>";
                result = runner.Run(null, new TestFilter(filterXml));
            }
            watch.Stop();
            cancellation.ThrowIfCancellationRequested();
            XmlNode[] selected = result.SelectNodes("//test-case").Cast<XmlNode>().ToArray();
            if (selected.Length != 1 || TCTNUnitBridge.Read(selected[0], "fullname") != _fullName)
                throw new InvalidDataException("NUnit filtered result does not match selected TestId: " + _fullName);
            XmlNode node = selected[0];
            string outcome = TCTNUnitBridge.Read(node, "result");
            string label = TCTNUnitBridge.Read(node, "label");
            string failure = node.SelectSingleNode("failure/message") == null ?
                string.Empty : node.SelectSingleNode("failure/message").InnerText;
            string stack = node.SelectSingleNode("failure/stack-trace") == null ?
                string.Empty : node.SelectSingleNode("failure/stack-trace").InnerText;
            int nativeAssertCount;
            if (!int.TryParse(TCTNUnitBridge.Read(node, "asserts"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out nativeAssertCount)) nativeAssertCount = 0;
            TCTTestStatus mapped = TCTNUnitBridge.MapStatus(outcome, label, nativeAssertCount);
            string error = failure + (string.IsNullOrWhiteSpace(stack) ? "" : Environment.NewLine + stack);
            if (mapped != TCTTestStatus.Passed && string.IsNullOrWhiteSpace(error))
                error = "NUnit outcome=" + outcome + "; label=" + label + "; asserts=" + nativeAssertCount;
            IList<TCTAssertionResult> assertions = new List<TCTAssertionResult> {
                new TCTAssertionResult("NUnit engine verdict (not individual native assertions)",
                    "Passed", outcome, mapped == TCTTestStatus.Passed)
            };
            IList<TCTTestObservation> observations = new List<TCTTestObservation> {
                new TCTTestObservation("NUnit", _fullName,
                    "NativeAssertCount=" + nativeAssertCount + "; Result=" + outcome + "; Label=" + label)
            };
            IList<TCTTestDiagnostic> diagnostics = new List<TCTTestDiagnostic>();
            if (mapped != TCTTestStatus.Passed)
                diagnostics.Add(new TCTTestDiagnostic("TCT.NUNIT." + (label == "" ? outcome : label),
                    mapped == TCTTestStatus.Failed || mapped == TCTTestStatus.Error ?
                        TCTTestDiagnosticSeverity.Error : TCTTestDiagnosticSeverity.Warning,
                    "NUnit.Engine", error, attempt == null ? "AdHoc" : attempt.AttemptId));
            IList<TCTArtifactRef> saved = new List<TCTArtifactRef>();
            if (artifacts != null)
                saved.Add(artifacts.Save(Encoding.UTF8.GetBytes(result.OuterXml),
                    "application/vnd.nunit.test-result+xml", attempt == null ? "AdHoc" : attempt.AttemptId));
            return TCTTestCaseResult.FromTransport(Descriptor, mapped, watch.Elapsed, error,
                assertions, observations, attempt, saved, diagnostics);
        }
    }

    public static class TCTNUnitBridge
    {
        public static string Fingerprint(string file)
        {
            using (FileStream stream = new FileStream(file, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static TCTTestStatus MapStatus(string result, string label, int nativeAsserts)
        {
            if (result == "Passed") return nativeAsserts > 0 ? TCTTestStatus.Passed : TCTTestStatus.Inconclusive;
            if (result == "Inconclusive" || label == "Inconclusive") return TCTTestStatus.Inconclusive;
            if (result == "Skipped") return TCTTestStatus.Skipped;
            if (label == "Cancelled") return TCTTestStatus.Cancelled;
            if (result == "Failed") return label == "Error" || label == "Invalid" ?
                TCTTestStatus.Error : TCTTestStatus.Failed;
            return TCTTestStatus.Error; // unexpected engine payloads fail closed
        }
        public static TestPackage CreatePackage(string assemblyPath)
        {
            TestPackage package = new TestPackage(assemblyPath);
            package.Settings["ProcessModel"] = "InProcess";
            package.Settings["DomainUsage"] = "None";
            return package;
        }
        public static string Read(XmlNode node, string attribute)
        {
            return node == null || node.Attributes == null || node.Attributes[attribute] == null ?
                string.Empty : node.Attributes[attribute].Value;
        }
    }
}
