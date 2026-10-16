using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using NUnit.Engine;
using NUnit.Framework;

namespace CxVisual.TestCenter.NUnitIntegration
{
    // Integration probe, not a substitute for TCT industrial orchestration or evidence.
    // Uses the officially supported NUnit Engine API, never reflection over internal engine types.
    public static class TCTNUnitEngineProbe
    {
        public static int Execute(string reportPath, bool expectFailure)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
                throw new ArgumentException("Report path is required", "reportPath");
            // The fixture exposes one intentional negative control only when explicitly requested.
            string traceFile = reportPath + ".trace.txt";
            Action<string> trace = stage => File.AppendAllText(traceFile,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + stage + Environment.NewLine);
            trace("Start " + typeof(TCTNUnitProbeFixture).Assembly.Location);
            string previous = Environment.GetEnvironmentVariable("TCT_NUNIT_EXPECT_NEGATIVE");
            Environment.SetEnvironmentVariable("TCT_NUNIT_EXPECT_NEGATIVE", expectFailure ? "1" : "0");
            try
            {
                trace("Before CreateInstance");
                using (ITestEngine engine = TestEngineActivator.CreateInstance())
                {
                    trace("Engine created");
                    TestPackage package = new TestPackage(typeof(TCTNUnitProbeFixture).Assembly.Location);
                    package.Settings["ProcessModel"] = "InProcess";
                    package.Settings["DomainUsage"] = "None";
                    trace("Before GetRunner");
                    using (ITestRunner runner = engine.GetRunner(package))
                    {
                        trace("Before Explore");
                        XmlNode discovered = runner.Explore(TestFilter.Empty);
                        trace("Explore done");
                        XmlNode[] found = discovered.SelectNodes("//test-case")
                            .Cast<XmlNode>().ToArray();
                        if (found.Length != 5)
                            throw new InvalidDataException("NUnit discovery: expected five cases, got " + found.Length);
                        trace("Before Run");
                        XmlNode run = runner.Run(null, TestFilter.Empty);
                        trace("Run done");
                        string parent = Path.GetDirectoryName(Path.GetFullPath(reportPath));
                        if (!Directory.Exists(parent)) Directory.CreateDirectory(parent);
                        File.WriteAllText(reportPath, run.OuterXml);
                        XmlNode[] cases = run.SelectNodes("//test-case").Cast<XmlNode>().ToArray();
                        if (cases.Length != 5)
                            throw new InvalidDataException("NUnit execution returned " + cases.Length + " cases");
                        int failed = cases.Count(c => Read(c, "result") == "Failed");
                        int passed = cases.Count(c => Read(c, "result") == "Passed");
                        int expectedFailures = expectFailure ? 1 : 0;
                        if (failed != expectedFailures || passed != 5 - expectedFailures)
                            throw new InvalidDataException("NUnit result mismatch: passed=" +
                                passed + ", failed=" + failed);
                        XmlNode negative = cases.Single(c => Read(c, "name") == "NegativeControl");
                        if (expectFailure && (Read(negative, "result") != "Failed" ||
                            !negative.InnerText.Contains("TCT_NUNIT_NEGATIVE_CONTROL")))
                            throw new InvalidDataException("NUnit intentional failure lost original assertion");
                        trace("Verified");
                        return 0;
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("TCT_NUNIT_EXPECT_NEGATIVE", previous);
            }
        }

        private static string Read(XmlNode node, string attribute)
        {
            return node.Attributes == null || node.Attributes[attribute] == null ?
                string.Empty : node.Attributes[attribute].Value;
        }
    }

    [TestFixture]
    public sealed class TCTNUnitProbeFixture
    {
        private int _initialized;

        [SetUp]
        public void Prepare() { _initialized = 42; }

        [TearDown]
        public void Cleanup() { _initialized = 0; }

        [Test]
        public void SyncAssertion()
        {
            Assert.That(_initialized, Is.EqualTo(42));
        }

        [Test]
        public async Task AsyncAssertion()
        {
            await Task.Delay(1).ConfigureAwait(false);
            Assert.That(_initialized, Is.EqualTo(42));
        }

        [TestCase(1, 2, 3)]
        [TestCase(6, 7, 13)]
        public void Parameterized(int left, int right, int expected)
        {
            Assert.That(left + right, Is.EqualTo(expected));
        }

        [Test]
        public void NegativeControl()
        {
            string control = Environment.GetEnvironmentVariable("TCT_NUNIT_EXPECT_NEGATIVE");
            if (control == "1") Assert.Fail("TCT_NUNIT_NEGATIVE_CONTROL");
            if (control == "ignored") Assert.Ignore("TCT_NUNIT_IGNORED_CONTROL");
            if (control == "inconclusive") Assert.Inconclusive("TCT_NUNIT_INCONCLUSIVE_CONTROL");
            if (control == "error") throw new InvalidOperationException("TCT_NUNIT_ERROR_CONTROL");
            Assert.That(true, Is.True);
        }
    }
}
