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
            TCTGeometryFigure expected = new TCTGeometryFigure("Point", "Expected",
                "预期命中点", "FGEPoint2D", expectedX, expectedY, 0, 0,
                null, "Geometry2D", "unitless");
            TCTGeometryFigure actual = new TCTGeometryFigure("Point", "Actual",
                "实际命中点", "FGEPoint2D", probe.X, probe.Y, 0, 0,
                null, "Geometry2D", "unitless");
            TCTGeometryDelta delta = TCTGeometryComparer.ComparePoints(expected, actual, 0.001);
            test.AssertEqual(0.0, delta.DeltaX, "Geometry变换X的实际误差");
            test.AssertEqual(0.0, delta.DeltaY, "Geometry变换Y的实际误差");
            test.AssertTrue(delta.WithinTolerance, "真实几何在0.001容差内",
                true, delta.WithinTolerance);
            TCTGeometryFigure[] figures = {
                new TCTGeometryFigure("Rectangle", "Input", "矩形定义", "FGERect2D",
                    rect.X, rect.Y, rect.Width, rect.Height,
                    null, "Geometry2D", "unitless"),
                expected, actual,
                new TCTGeometryFigure("Segment", "Diff", "二维误差向量", "FGEPoint2D",
                    expectedX, expectedY, delta.DeltaX, delta.DeltaY,
                    null, "Geometry2D", "unitless")
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
            var expectedPoint = new TCTGeometryFigure("Point", "Expected", "point",
                "FGEPoint2D", 0, 0, 0, 0, null, "Geometry2D", "mm");
            var actualPoint = new TCTGeometryFigure("Point", "Actual", "point",
                "FGEPoint2D", 1, 0, 0, 0, null, "Geometry2D", "mm");
            TCTGeometryDelta missed = TCTGeometryComparer.ComparePoints(
                expectedPoint, actualPoint, 0.01);
            test.AssertTrue(!missed.WithinTolerance && missed.Distance == 1,
                "几何超出容差不可标通过", true, !missed.WithinTolerance);
            var wrongUnits = new TCTGeometryFigure("Point", "Actual", "point",
                "FGEPoint2D", 1, 0, 0, 0, null, "Geometry2D", "pixel");
            test.AssertThrows<InvalidDataException>(() =>
                TCTGeometryComparer.ComparePoints(expectedPoint, wrongUnits, 0.01),
                "不同单位不可直接比较");
            test.AssertThrows<ArgumentOutOfRangeException>(() =>
                TCTGeometryComparer.ComparePoints(expectedPoint, actualPoint, double.NaN),
                "非法容差必须拒绝");
            // Legacy schema 1 lacked optional space/unit; replay stays backward compatible.
            XDocument oldSchema = XDocument.Parse(Encoding.UTF8.GetString(valid));
            oldSchema.Root.Element("Figure").Attribute("space").Remove();
            oldSchema.Root.Element("Figure").Attribute("unit").Remove();
            test.AssertEqual("Unspecified", TCTGeometryEvidence.Read(
                Encoding.UTF8.GetBytes(oldSchema.ToString()))[0].Unit,
                "旧版Geometry证据兼容");
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
