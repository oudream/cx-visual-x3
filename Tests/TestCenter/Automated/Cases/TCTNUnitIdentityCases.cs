using System;
using System.IO;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.NUnitIntegration;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTNUnitIdentityCases
    {
        [TCTTest("TCT.M1.NUNIT.001", "NUnit参数行与Definition独立身份",
            TCTTestAspect.Foundation, "TestFramework", ObjectId = "TCTNUnitTestCase")]
        public static void StableRows(TCTTestContext test)
        {
            string location = typeof(TCTNUnitIdentityCases).Assembly.Location;
            var first = new TCTNUnitTestCase(location, "TCT.Calculation.Add(2,3)", "Add(2,3)");
            var second = new TCTNUnitTestCase(location, "TCT.Calculation.Add(3,4)", "Add(3,4)");
            var rebuilt = new TCTNUnitTestCase(location, "TCT.Calculation.Add(2,3)", "Add(2,3)");
            test.AssertEqual(first.DefinitionId, second.DefinitionId,
                "同一NUnit测试定义");
            test.AssertNotEqual(first.CaseId, second.CaseId, "两参数行各自独立");
            test.AssertEqual(first.CaseId, rebuilt.CaseId, "重建的CaseId稳定");
            test.AssertEqual("(2,3)", first.RowKey, "参数行身份");
            test.AssertEqual(first.DefinitionFingerprint, rebuilt.DefinitionFingerprint,
                "相同程序集SHA256稳定");
            var external = new TCTNUnitTestCase(location,
                "TCT.Calculation.Add(2,3)", "Add(2,3)", null, "ExternalAssembly");
            test.AssertNotEqual(first.CaseId, external.CaseId,
                "外部程序集同名FullName不会误合并");
        }

        [TCTTest("TCT.M1.NUNIT.002", "NUnit发现后定义文件改变必须失败",
            TCTTestAspect.Foundation, "TestFramework", ObjectId = "TCTNUnitBridge")]
        public static void ChangedAssemblyRejected(TCTTestContext test)
        {
            string location = typeof(TCTNUnitIdentityCases).Assembly.Location;
            var wrong = new TCTNUnitTestCase(location,
                "CxVisual.TestCenter.NUnitIntegration.TCTNUnitProbeFixture.SyncAssertion",
                "SyncAssertion", new string('0', 64));
            test.AssertThrows<InvalidDataException>(() =>
                wrong.ExecuteExternal(null, System.Threading.CancellationToken.None, null),
                "不能执行与发现时指纹不同的测试");
        }
    }
}
