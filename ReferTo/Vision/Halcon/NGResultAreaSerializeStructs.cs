using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HalconDriverModule
{
    // 视觉检测项结果：检测结果状态、检测结果明细信息（如气泡检测结果：区域ID，气泡比，面积，高度，宽度)
    // 产品检测结果项：由多个检测项结果组成，（顺序必须以 LS_ResultStatuses 顺序保存）
    // 产品检测结果明细项：由多个检测项的明细合并成一个字符串，也能从一个字符串拆解出详细列表
    public static class VisionDetectionResultContentHelper
    {
        public static string SerializeDetectionResultDetail(ProductDetectionResultDetail result)
        {
            var parts = new List<string>();

            foreach (var bubble in result.EBRDetections)
            {
                parts.Add(bubble.ToLineString());
            }
            foreach (var overflow in result.BWDetections)
            {
                parts.Add(overflow.ToLineString());
            }
            foreach (var breakDetection in result.VoidDetections)
            {
                parts.Add(breakDetection.ToLineString());
            }

            return string.Join("|", parts);
        }

        public static ProductDetectionResultDetail DeserializeDetectionResultDetail(string data)
        {
            var result = new ProductDetectionResultDetail();
            if (string.IsNullOrEmpty(data)) return result;
            var parts = data.Split('|');

            foreach (var part in parts)
            {
                var details = part.Split(':');
                if (details.Length < 2) continue;
                var values = details[1].Split(',');

                switch (details[0])
                {
                    case "B":
                        result.EBRDetections.Add(EBRDetectionResultDetail.Parse(details[1]));
                        break;

                    case "O":
                        result.BWDetections.Add(BWDetectionResultDetail.Parse(details[1]));
                        break;

                    case "D":
                        result.VoidDetections.Add(VoidDetectionResultDetail.Parse(details[1]));
                        break;
                }
            }

            return result;
        }

        public static string SerializeDetectionAreaDetail(List<NGResultAreaDetail> results)
        {
            if (results.Count <= 0)
            {
                return "";
            }


            var parts = new List<string>();

            foreach (var res in results)
            {
                parts.Add(res.ToLine());
            }
            return string.Join("|", parts);
        }

        public static List<NGResultAreaDetail> DeserializeDetectionAreaDetail(string data, NGLevelType filterType = NGLevelType.NG)
        {
            var result = new List<NGResultAreaDetail>();
            var parts = data.Split('|');

            foreach (var part in parts)
            {
                var details = part.Split(',');
                if (details.Length < 7) continue;

                var detail = NGResultAreaDetail.Parse(part);
                if (detail.LeveType == filterType)
                {
                    result.Add(detail);
                }
            }

            return result;
        }

        public static List<NGResultAreaDetail> DeserializeDetectionAreaDetailALL(string data)
        {
            var result = new List<NGResultAreaDetail>();
            var parts = data.Split('|');

            foreach (var part in parts)
            {
                var details = part.Split(',');
                if (details.Length < 7) continue;

                var detail = NGResultAreaDetail.Parse(part);
                if (detail.NGName != "BWOoLSL" && detail.NGName != "EBROoLSL" && detail.NGName != "SpecialEBROoLSL" && detail.NGName != "MaxVoidAreaOoUSL" )
                {
                    result.Add(detail);
                }
            }

            return result;
        }

        public static NGResultAreaDetail GetMaxAreaNGResult(List<NGResultAreaDetail> ngResults, string ngName)
        {
            if (ngResults == null || ngResults.Count == 0)
            {
                return null;
            }

            return ngResults
                .Where(result => result.NGName.Equals(ngName))
                .OrderByDescending(result => result.Area)
                .FirstOrDefault();
        }

        public static NGResultAreaDetail GetMinAreaNGResult(List<NGResultAreaDetail> ngResults, string ngName)
        {
            if (ngResults == null || ngResults.Count == 0)
            {
                return null;
            }

            return ngResults
                .Where(result => result.NGName.Equals(ngName) && result.Area > 0)
                .OrderBy(result => result.Area)
                .FirstOrDefault();
        }
    }

    public class ProductDetectionResultDetail
    {
        /// <summary>
        /// 数据是OK时为false，详细NG列表都为0
        /// </summary>
        public bool NG { get; set; }
        /// <summary>
        /// 检测失败时为true
        /// </summary>
        public bool DectFail { get; set; }
        public uint BatchNumber { get; set; }
        public int Position { get; set; }
        public List<EBRDetectionResultDetail> EBRDetections { get; set; } = new List<EBRDetectionResultDetail>();
        public List<BWDetectionResultDetail> BWDetections { get; set; } = new List<BWDetectionResultDetail>();
        public List<VoidDetectionResultDetail> VoidDetections { get; set; } = new List<VoidDetectionResultDetail>();

        public void InitState()
        {
            NG = EBRDetections.Any(item => item != null && item.IsNG()) || BWDetections.Count >= 0 || VoidDetections.Count >= 0;
        }


        public string GetUserAllResultText()
        {
            string text = "";
            if(EBRDetections!=null && EBRDetections.Count > 0) 
            {
                foreach (var item in EBRDetections)
                {
                    text += "EBR:" + item.PartID + "," + item.BubbleRatio + ",";
                }
            }
            if(BWDetections!=null && BWDetections.Count > 0) 
            {
                foreach(var item in BWDetections) 
                {
                    text+="Bw:" + item.PartID + "," + item.Area + ",";
                }
            }
            if(VoidDetections!=null && VoidDetections.Count > 0) 
            {
                foreach(var item in VoidDetections) 
                {
                    text+="Void:" + item.PartID + "," + item.Area + ",";
                }
            }
            return text;
        }

        public VoidDetectionResultDetail GetMaxVoidDetect(double vpt)
        {
            if (VoidDetections == null || VoidDetections.Count <= 0)
            {
                return null;
            }
            var val = VoidDetections.Where(item => item.Area > vpt).OrderByDescending(item => item.Area).FirstOrDefault();
            return val;
        }

        public EBRDetectionResultDetail GetMinBubbleDetect()
        {
            if (EBRDetections == null || EBRDetections.Count <= 0)
            {
                return null;
            }
            var val = EBRDetections.Where(item => item.BubbleRatio < item.Partlsl && item.BubbleRatio >= 0).OrderBy(item => item.BubbleRatio).FirstOrDefault();
            return val;
        }

        public BWDetectionResultDetail GetMinBWDetect(double vpt)
        {
            if (BWDetections == null || BWDetections.Count <= 0)
            {
                return null;
            }
            var val = BWDetections.Where(item => item.Area < vpt && item.Area >= 0).OrderBy(item => item.Area).FirstOrDefault();
            return val;
        }

        //人工判定不需要判定大小,所有的都认为是
        public VoidDetectionResultDetail UserGetMaxVoidDetect(double vpt)
        {
            if (VoidDetections == null || VoidDetections.Count <= 0)
            {
                return null;
            }
            var val = VoidDetections.Where(item => item.Area > 0).OrderByDescending(item => item.Area).FirstOrDefault();
            return val;
        }

        public EBRDetectionResultDetail UserGetMinBubbleDetect()
        {
            if (EBRDetections == null || EBRDetections.Count <= 0)
            {
                return null;
            }
            var val = EBRDetections.Where(item => item.BubbleRatio >= 0).OrderBy(item => item.BubbleRatio).FirstOrDefault();
            return val;
        }

        public BWDetectionResultDetail UserGetMinBWDetect(double vpt)
        {
            if (BWDetections == null || BWDetections.Count <= 0)
            {
                return null;
            }
            var val = BWDetections.Where(item => item.Area >= 0).OrderBy(item => item.Area).FirstOrDefault();
            return val;
        }

    }

    // EBR测结果：区域ID，EBR比例，面积，高度，宽度
    public class EBRDetectionResultDetail
    {
        public int PartID { get; set; }
        public double BubbleRatio { get; set; }
        public double Area { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        // 分区判定类型 0:标准区, 1:非标准区
        public double Partlsl { get; set; }
        public string ToLineString() { return Format(PartID, BubbleRatio, Area, Height, Width, Partlsl); }
        public static string Format(int partID, double bubbleRatio, double area, double height, double width, double partlsl)
        {
            return $"B:{partID},{bubbleRatio:0.###},{area:0.###},{height:0.###},{width:0.###},{partlsl}";
        }
        public bool IsNG() { return BubbleRatio < Partlsl; }
        public static EBRDetectionResultDetail Parse(string line)
        {
            List<string> values = line.Split(',').ToList();
            if (values.Count() < 6)
            {
                values.Add("0.5");
            }


            return new EBRDetectionResultDetail
            {
                PartID = int.Parse(values[0]),
                BubbleRatio = double.Parse(values[1]),
                Area = double.Parse(values[2]),
                Height = double.Parse(values[3]),
                Width = double.Parse(values[4]),
                Partlsl = double.Parse(values[5])
            };
        }
        public EBRDetectionResultDetail()
        {
        }
        public EBRDetectionResultDetail(int areaID, double bubbleRatio, double area, double height, double width, double partUsl)
        {
            PartID = areaID;
            BubbleRatio = bubbleRatio;
            Area = area;
            Height = height;
            Width = width;
            Partlsl = partUsl;
        }
    }

    // 胶宽小检测结果：区域ID，面积，高度，宽度
    public class BWDetectionResultDetail
    {
        // 同一产品的第几个溢胶区域
        public int PartID { get; set; }
        public double Area { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public string ToLineString() { return Format(PartID, Area, Height, Width); }
        public static string Format(int partID, double area, double height, double width)
        {
            return $"O:{partID},{area:0.###},{height:0.###},{width:0.###}";
        }
        public static BWDetectionResultDetail Parse(string line)
        {
            var values = line.Split(',');
            return new BWDetectionResultDetail
            {
                PartID = int.Parse(values[0]),
                Area = double.Parse(values[1]),
                Height = double.Parse(values[2]),
                Width = double.Parse(values[3])
            };
        }

        public BWDetectionResultDetail()
        {
        }

        public BWDetectionResultDetail(int partID, double area, double height, double width)
        {
            PartID = partID;
            Area = area;
            Height = height;
            Width = width;
        }
    }

    // 气泡检测结果：区域ID，面积，高度，宽度
    public class VoidDetectionResultDetail
    {
        public int PartID { get; set; }
        public double Area { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public string ToLineString() { return Format(PartID, Area, Height, Width); }
        public static string Format(int partID, double area, double height, double width)
        {
            return $"D:{partID},{area:0.###},{height:0.###},{width:0.###}";
        }
        public static VoidDetectionResultDetail Parse(string line)
        {
            var values = line.Split(',');
            return new VoidDetectionResultDetail
            {
                PartID = int.Parse(values[0]),
                Area = double.Parse(values[1]),
                Height = double.Parse(values[2]),
                Width = double.Parse(values[3])
            };
        }

        public VoidDetectionResultDetail()
        {
        }

        public VoidDetectionResultDetail(int partID, double area, double height, double width)
        {
            PartID = partID;
            Area = area;
            Height = height;
            Width = width;
        }

    }

    public enum NGLevelType
    {
        NG = 0, //NG类型
        OK = 1, //OK类型
    }

    /// <summary>
    /// 人工缺陷所属区域类型（用于输出侧动态分区计算）。
    /// </summary>
    public enum DefectAreaType
    {
        UnKnow = 0,
        GluePathArea = 1,
        GridArea = 2
    }

    /// <summary>
    /// 人工缺陷区域辅助类。
    /// 仅依赖 NGResultAreaDetail，本身不参与数据库存储逻辑。
    /// </summary>
    public static class UserDefectAreaHelper
    {
        /// <summary>
        /// 根据缺陷名称判断所属区域类型。
        /// </summary>
        public static DefectAreaType ResolveDefectAreaType(NGResultAreaDetail detail)
        {
            if (detail == null)
            {
                return DefectAreaType.UnKnow;
            }
            return ResolveDefectAreaType(detail.NGName);
        }

        /// <summary>
        /// 根据缺陷名称判断所属区域类型。
        /// </summary>
        public static DefectAreaType ResolveDefectAreaType(string defectName)
        {
            if (string.IsNullOrWhiteSpace(defectName))
            {
                return DefectAreaType.UnKnow;
            }

            if (defectName.Contains("BW")|| defectName.Contains("Void")|| defectName.Contains("EBR"))
            {
                return DefectAreaType.GluePathArea;
            }

            return DefectAreaType.GridArea;
        }

        /// <summary>
        /// 动态计算分区编号（输出侧调用）。
        /// 调用方可按区域类型传入具体计算函数；未提供时返回 0。
        /// </summary>
        public static int ResolvePartitionId(
            NGResultAreaDetail detail,
            Func<NGResultAreaDetail, int> gluePathResolver = null,
            Func<NGResultAreaDetail, int> gridResolver = null)
        {
            if (detail == null)
            {
                return 0;
            }

            var areaType = ResolveDefectAreaType(detail);
            switch (areaType)
            {
                case DefectAreaType.GluePathArea:
                    return gluePathResolver != null ? gluePathResolver(detail) : 0;
                case DefectAreaType.GridArea:
                    return gridResolver != null ? gridResolver(detail) : 0;
                default:
                    return 0;
            }
        }
    }

    /// <summary>
    /// NG缺陷区域详细信息
    /// </summary>
    public class NGResultAreaDetail
    {
        /// <summary>
        /// AI缺陷中的ID
        /// </summary>
        public int BBoxId { get; set; }

        /// <summary>
        /// 缺陷名称，英文
        /// </summary>
        public string NGName { get; set; }
        public double Area { get; set; }
        public double Height { get; set; }
        public double Width { get; set; }
        public double Row { get; set; }
        public double Col { get; set; }
        public NGLevelType LeveType { get; set; } = NGLevelType.NG;// 0: NG 1:OK { get; set; }
        public string ToLine()
        {
            return $"{NGName},{BBoxId},{Area:0.###},{Height:0.###},{Width:0.###},{Row:0.###},{Col:0.###},{(int)LeveType}";
        }

        public static NGResultAreaDetail Parse(string line)
        {
            var values = line.Split(',').ToList();

            if (values.Count < 8)
            {
                values.Add("0"); //添加 NGLevelType.NG
            }
            return new NGResultAreaDetail
            {
                NGName = values[0],
                BBoxId = int.Parse(values[1]),
                Area = double.Parse(values[2]),
                Height = double.Parse(values[3]),
                Width = double.Parse(values[4]),
                Row = double.Parse(values[5]),
                Col = double.Parse(values[6]),
                LeveType = (NGLevelType)int.Parse(values[7]),
            };
        }

        public static string GetNGLeveDarwColor(NGLevelType levelType)
        {
            if (levelType == NGLevelType.NG)
            {
                return "red";
            }
            else
            {
                return "yellow";
            }
        }
    }

}
