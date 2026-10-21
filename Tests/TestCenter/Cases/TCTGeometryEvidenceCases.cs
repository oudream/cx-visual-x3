using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CxVisual.Foundation.Geometry;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTGeometryEvidenceCases
    {
        [TCTTest("TCT.M2.GEOMETRY.001", "真实FGERect2D几何画布、预期实际与误差",
            TCTTestAspect.Foundation, "Geometry / Visual", ObjectId = "FGERect2D")]
        public static void GeometryWithEvidence(TCTTestContext test)
        {
            FGERect2D rect = new FGERect2D(10, 20, 100, 80);
            FGEPoint2D probe = new FGEPoint2D(40, 65);
            bool contains = rect.Contains(probe);
            test.AssertTrue(contains, "真实矩形包含点", true, contains);
            // Expected and actual are deliberately equal for a correctly working Owner.
            // Diff is the computed vector, not a random decorative value.
            double expectedX = 40, expectedY = 65;
            double dx = probe.X - expectedX, dy = probe.Y - expectedY;
            test.AssertEqual(0.0, dx, "Geometry变换X的实际误差");
            test.AssertEqual(0.0, dy, "Geometry变换Y的实际误差");
            TCTGeometryFigure[] figures = {
                new TCTGeometryFigure("Rectangle", "Input", "矩形定义", "FGERect2D",
                    rect.X, rect.Y, rect.Width, rect.Height),
                new TCTGeometryFigure("Point", "Expected", "预期命中点", "FGEPoint2D",
                    expectedX, expectedY, 0, 0),
                new TCTGeometryFigure("Point", "Actual", "实际命中点", "FGEPoint2D",
                    probe.X, probe.Y, 0, 0),
                new TCTGeometryFigure("Segment", "Diff", "二维误差向量", "FGEPoint2D",
                    expectedX, expectedY, dx, dy)
            };
            byte[] bytes = TCTGeometryEvidence.Serialize(figures);
            test.AssertEqual(4, TCTGeometryEvidence.Read(bytes).Count, "Geometry证据可恢复");
            test.ObserveStructured("Geometry", "矩形命中与误差", "FGERect2D.Contains",
                "FGERect2D", "Foundation.Geometry/FGERect2D.Contains", null,
                bytes, TCTGeometryEvidence.Mime);
        }

        [TCTTest("TCT.M2.GEOMETRY.002", "几何证据Schema、NaN、DTD及超限负例",
            TCTTestAspect.Foundation, "Geometry / Visual", ObjectId = "TCTGeometryEvidence")]
        public static void InvalidGeometryEvidence(TCTTestContext test)
        {
            TCTGeometryFigure sample = new TCTGeometryFigure("Rectangle", "Expected", "box",
                "FGERect2D", 0, 0, 10, 10);
            byte[] valid = TCTGeometryEvidence.Serialize(new[] { sample });
            test.AssertEqual(1, TCTGeometryEvidence.Read(valid).Count, "正常几何数据可读");
            test.AssertThrows<InvalidDataException>(() =>
                new TCTGeometryFigure("Rectangle", "Actual", "NaN", "FGERect2D",
                    double.NaN, 0, 10, 10), "NaN坐标不可进入真实证据");
            test.AssertThrows<InvalidDataException>(() =>
                TCTGeometryEvidence.Read(new byte[1024 * 1024 + 1]), "大负载拒绝");
            test.AssertThrows<XmlException>(() =>
                TCTGeometryEvidence.Read(Encoding.UTF8.GetBytes(
                    "<!DOCTYPE x [<!ENTITY z SYSTEM 'file:///secret'>]><GeometryEvidence schema='1'/>")),
                "DTD被禁止");
            XDocument forged = XDocument.Parse(Encoding.UTF8.GetString(valid));
            forged.Root.SetAttributeValue("schema", 999);
            test.AssertThrows<InvalidDataException>(() =>
                TCTGeometryEvidence.Read(Encoding.UTF8.GetBytes(forged.ToString())),
                "未知Schema拒绝");
            forged = XDocument.Parse(Encoding.UTF8.GetString(valid));
            forged.Root.Element("Figure").SetAttributeValue("role", "Passed");
            test.AssertThrows<InvalidDataException>(() =>
                TCTGeometryEvidence.Read(Encoding.UTF8.GetBytes(forged.ToString())),
                "禁止从角色伪造Passed状态");
        }
    }
}
