using System;
using System.IO;
using System.Text;
using System.Xml;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTWireSafetyCases
    {
        [TCTTest("TCT.M1.WIRE.001", "Worker结果XML禁止外部实体与无边界大小",
            TCTTestAspect.Foundation, "TestFramework", ObjectId = "TCTWireCodec")]
        public static void DtdAndUntrustedXml(TCTTestContext test)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                string path = sandbox.PathFor("malicious.xml");
                File.WriteAllText(path,
                    "<!DOCTYPE x [<!ENTITY ext SYSTEM 'file:///C:/Windows/win.ini'>]>" +
                    "<TestResult schema='1' id='TCT.LOCAL.XML' status='Passed'>" +
                    "<Error>&ext;</Error></TestResult>", Encoding.UTF8);
                var descriptor = new TCTTestCaseDescriptor("TCT.LOCAL.XML", "xml",
                    TCTTestAspect.Foundation, "TestFramework", "Unmapped", "Unmapped", "TCTWireCodec");
                test.AssertThrows<XmlException>(() => TCTWireCodec.Read(path, descriptor),
                    "DTD/外部实体绝不能被Worker结果导入");
            }
        }
    }
}
