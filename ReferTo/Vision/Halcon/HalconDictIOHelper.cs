using CommonInterfaces;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule
{
    public static class HalconDictIOHelper
    {
        public static T TupleConvertTo<T>(HTuple obj, T defaultValue = default(T))
        {
            if (obj == null || obj.Length == 0)
                return defaultValue;

            object value = null;
            switch (obj.Type)
            {
                case HTupleType.INTEGER:
                    value = obj.I;
                    break;
                case HTupleType.LONG:
                    value = obj.L;
                    break;
                case HTupleType.DOUBLE:
                    value = obj.D;
                    break;
                case HTupleType.STRING:
                    value = obj.S;
                    break;
                default:
                    return defaultValue;
            }
            return (T)Convert.ChangeType(value, typeof(T));
        }


        public static HObject GetDrawDictAryRegion(HTuple hv_DrawDictAry, string typeName)
        {
            if (hv_DrawDictAry == null || hv_DrawDictAry.Length == 0)
                return null;

            for (int idx = 0; idx < hv_DrawDictAry.Length; idx++)
            {
                var hv_DictHandle = hv_DrawDictAry[idx];

                // 获取Type字段
                HOperatorSet.GetDictTuple(hv_DictHandle, "Type", out HTuple hv_Type);
                if (hv_Type != null && hv_Type.S == typeName)
                {
                    // 获取Region对象
                    HOperatorSet.GetDictObject(out HObject ho_BrazingPath, hv_DictHandle, "Region");
                    return ho_BrazingPath;
                }
            }

            return null;
        }

        public static double GetMinDrawDictAry(HTuple hv_DrawDictAry, string ngTypeName)
        {
            if (hv_DrawDictAry == null || hv_DrawDictAry.Length == 0)
                return 0;

            double minBrazingThickness = double.MaxValue;

            for (int idx = 0; idx < hv_DrawDictAry.Length; idx++)
            {
                var hv_DictHandle = hv_DrawDictAry[idx];

                // 获取Type字段
                HOperatorSet.GetDictTuple(hv_DictHandle, "Type", out HTuple hv_Type);
                if (hv_Type == null || hv_Type.S != ngTypeName)
                    continue;

                // 获取Area字段
                HOperatorSet.GetDictTuple(hv_DictHandle, "Area", out HTuple hv_Area);
                if (hv_Area != null)
                {
                    double area = hv_Area.D;
                    if (area < minBrazingThickness)
                        minBrazingThickness = area;
                }
            }

            return (minBrazingThickness == double.MaxValue) ? 0 : minBrazingThickness;
        }

        // 使用 double maxVoid = 0; 的方式，可以把小于等于0的值过滤掉
        public static double GetMaxDrawDictAry(HTuple hv_DrawDictAry, string ngTypeName)
        {
            if (hv_DrawDictAry == null || hv_DrawDictAry.Length == 0)
                return 0;

            double maxVoid = 0;

            for (int idx = 0; idx < hv_DrawDictAry.Length; idx++)
            {
                var hv_DictHandle = hv_DrawDictAry[idx];

                // 获取Type字段
                HOperatorSet.GetDictTuple(hv_DictHandle, "Type", out HTuple hv_Type);
                if (hv_Type == null || hv_Type.S != ngTypeName)
                    continue;

                // 获取Area字段
                HOperatorSet.GetDictTuple(hv_DictHandle, "Area", out HTuple hv_Area);
                if (hv_Area != null)
                {
                    double area = hv_Area.D;
                    if (area > maxVoid)
                        maxVoid = area;
                }
            }

            return maxVoid;
        }

        public static bool TryGetDictValueSafe(HTuple dict, string key, out HTuple value)
        {
            value = null;

            if (dict == null || dict.Length == 0 || string.IsNullOrEmpty(key))
                return false;

            try
            {
                // 只查一次 keys
                HOperatorSet.GetDictParam(dict, "keys", new string[] { key }, out HTuple keys);

                // 判断是否存在 key
                if (keys.TupleFind(key) >= 0)
                {
                    HOperatorSet.GetDictTuple(dict, key, out value);
                    return true;
                }
            }
            catch
            {
                // 安全处理
            }

            return false;
        }

        public static double GetOutDictAryForDouble(HTuple hv_OutDictAry, string key)
        {
            var value = GetOutDictAryForObject(hv_OutDictAry, key);
            if (value != null) return value.D;
            return 0;
        }

        public static int GetOutDictAryForInt(HTuple hv_OutDictAry, string key)
        {
            var value = GetOutDictAryForObject(hv_OutDictAry, key);
            if (value != null) return value.I;
            return 0;
        }

        public static string GetOutDictAryForString(HTuple hv_OutDictAry, string key)
        {
            var value = GetOutDictAryForObject(hv_OutDictAry, key);
            if (value != null) return value.S;
            return string.Empty;
        }

        public static HTuple GetOutDictAryForObject(HTuple hv_OutDictAry, string key)
        {
            if (hv_OutDictAry == null || hv_OutDictAry.Length == 0 || string.IsNullOrWhiteSpace(key))
                return null;

            // 判断 key 是否存在
            HOperatorSet.GetDictParam(hv_OutDictAry, "key_exists", new HTuple(key), out HTuple exists);
            if (exists.Length == 0 || exists[0].I != 1)
                return null;

            // 获取值
            HOperatorSet.GetDictTuple(hv_OutDictAry, key, out var value);

            return value;
        }

        public static bool ExistOutDictAryForObject(HTuple hv_OutDictAry, string key)
        {
            if (hv_OutDictAry == null || hv_OutDictAry.Length == 0 || string.IsNullOrWhiteSpace(key))
                return false;

            // 判断 key 是否存在
            HOperatorSet.GetDictParam(hv_OutDictAry, "key_exists", new HTuple(key), out HTuple exists);
            if (exists.Length == 0 || exists[0].I != 1)
                return false;

            return true;
        }

        public static bool GetBiggestBubbleNGValues(HTuple hv_BiggestBubble, out int ngBW, out int ngVoid)
        {
            ngBW = 0;
            ngVoid = 0;
            if (hv_BiggestBubble != null && hv_BiggestBubble.Length > 3)
            {
                // 使用十进制数值（个位表示是否一般断胶NG，十位表示是否大气泡断胶NG）
                int ngsValue = hv_BiggestBubble[3].I;
                // 取个位值
                ngBW = ngsValue % 10;
                // 取十位值
                ngVoid = (ngsValue / 10) % 10;
                //// 取百位值
                //int ng3 = (ngsValue / 100) % 10;
                // 返回对应的字符串
                return true;
            }

            return false;
        }

        public static void DrawClassicalVisionDrawDictAry(HTuple windowID, HTuple hv_DrawDictAry, string typeName)
        {
            if (windowID == null || hv_DrawDictAry == null)
            {
                return;
            }
            for (int idx = 0; idx < hv_DrawDictAry.Length; idx++)
            {
                var hv_DictHandle = hv_DrawDictAry[idx];
                                                                                        // 获取Type字段
                HOperatorSet.GetDictTuple(hv_DictHandle, "Type", out HTuple hv_Type);
                if (hv_Type != null && hv_Type.S == typeName)
                {
                    HOperatorSet.GetDictTuple(hv_DictHandle, "Color", out var hv_Color); // 绘制的颜色
                    HOperatorSet.GetDictObject(out var ho_Region, hv_DictHandle, "Region"); // 绘制的区域
                    HOperatorSet.SetColor(windowID, hv_Color);
                    HOperatorSet.DispObj(ho_Region, windowID);
                }
            }
        }

        public static void DrawClassicalVisionDrawDictAry(HTuple windowID, HTuple hv_DrawDictAry)
        {
            if (windowID == null || hv_DrawDictAry == null)
            {
                return;
            }
            for (int idx = 0; idx < hv_DrawDictAry.Length; idx++)
            {
                var hv_DictHandle = hv_DrawDictAry[idx];
                HOperatorSet.GetDictTuple(hv_DictHandle, "Color", out var hv_Color); // 绘制的颜色
                HOperatorSet.GetDictObject(out var ho_Region, hv_DictHandle, "Region"); // 绘制的区域

                HOperatorSet.SetColor(windowID, hv_Color);
                HOperatorSet.DispObj(ho_Region, windowID);
            }
        }

        public static void DrawClassicalVisionDrawDictAry(HalconWindowTools hWindowTools, HTuple hv_DrawDictAry)
        {
            if (hWindowTools == null || hv_DrawDictAry == null)
            {
                return;
            }
            for (int i = 0; i < hv_DrawDictAry.Length; i++)
            {
                var hv_DictHandle = hv_DrawDictAry[i];
                HOperatorSet.GetDictTuple(hv_DictHandle, "Color", out var hv_Color); // 绘制的颜色
                HOperatorSet.GetDictObject(out var ho_Region, hv_DictHandle, "Region"); // 绘制的区域

                hWindowTools.AddDispObject(ho_Region, hv_Color.S, "margin", 0);
            }
        }

        public static void DisposeClassicalVisionDrawDictAry(HTuple hv_DrawDictAry)
        {
            if (hv_DrawDictAry != null)
            {
                for (int idx = 0; idx < hv_DrawDictAry.Length; idx++)
                {
                    var hv_DictHandle = hv_DrawDictAry[idx];
                    HOperatorSet.GetDictObject(out var ho_Region, hv_DictHandle, "Region"); //断胶位置
                    ho_Region.Dispose();
                }
            }
        }

        public static void DrawClassicalVisionTextDictAry(HTuple windowID, HTuple hv_TextDictAry)
        {
            if (windowID == null || hv_TextDictAry == null)
            {
                return;
            }

            for (int idx = 0; idx < hv_TextDictAry.Length; idx++)
            {
                var hv_DictHandle = hv_TextDictAry[idx];
                HOperatorSet.GetDictTuple(hv_DictHandle, "Color", out var hv_Color); // 绘制的颜色
                HOperatorSet.GetDictTuple(hv_DictHandle, "Text", out var hv_Text); // 绘制的文件
                HOperatorSet.GetDictTuple(hv_DictHandle, "Row", out var hv_Row); // 绘制的行
                HOperatorSet.GetDictTuple(hv_DictHandle, "Column", out var hv_Column); // 绘制的列
                                                                                       // 绘制
                HOperatorSet.DispText(windowID, hv_Text, "image", hv_Row, hv_Column, hv_Color, "box", "false");
            }
        }

        public static void DrawClassicalVisionTextDictAry(HalconWindowTools hWindowTools, HTuple hv_TextDictAry)
        {
            if (hWindowTools == null || hv_TextDictAry == null)
            {
                return;
            }
            for (int i = 0; i < hv_TextDictAry.Length; i++)
            {
                var hv_DictHandle = hv_TextDictAry[i];
                HOperatorSet.GetDictTuple(hv_DictHandle, "Color", out var hv_Color); // 绘制的颜色
                HOperatorSet.GetDictTuple(hv_DictHandle, "Text", out var hv_Text); // 绘制的文件
                HOperatorSet.GetDictTuple(hv_DictHandle, "Row", out var hv_Row); // 绘制的行
                HOperatorSet.GetDictTuple(hv_DictHandle, "Column", out var hv_Column); // 绘制的列
                hWindowTools.AddNewDrawFont(hv_Text.S, hv_Color.S, TupleConvertTo<int>(hv_Row), TupleConvertTo<int>(hv_Column));
            }
        }

        // 分析出EBR缺陷、序列化
        public static void ParseClassicalVisionEBR(HTuple hv_EBRDictAry, List<double> minEBRs, List<string> names, Dictionary<string, string> labelNames, List<NGResultAreaDetail> ngDetails, ProductDetectionResultDetail resultDetails)
        {
            // BW, EBR  判断与序列化
            // sEBRContent1, sEBRContent2
            if (hv_EBRDictAry != null)
            {
                for (int i = 0; i < hv_EBRDictAry.Length; i++)
                {
                    var hv_Dict = hv_EBRDictAry[i];
                    HOperatorSet.GetDictTuple(hv_Dict, "Product", out var hv_Product); //A or B
                    HOperatorSet.GetDictTuple(hv_Dict, "PartType", out var hv_PartType); // 0:标准区 1:非标准区
                    HOperatorSet.GetDictTuple(hv_Dict, "Id", out var hv_Id); //第几区域
                    HOperatorSet.GetDictTuple(hv_Dict, "EBR", out var hv_EBR);  // EBR 
                    HOperatorSet.GetDictTuple(hv_Dict, "MinT", out var MinT);  // 最小胶宽
                    HOperatorSet.GetDictTuple(hv_Dict, "BubbleArea", out var BubbleArea);  // 最大气泡
                    HOperatorSet.GetDictTuple(hv_Dict, "FontRow", out var FontRow);
                    HOperatorSet.GetDictTuple(hv_Dict, "FontCol", out var FontCol);
                    HOperatorSet.GetDictTuple(hv_Dict, "Type", out var Type); //EBR结果字符
                    double ebr = hv_EBR.D;

                    // ebr
                    var idx = hv_PartType.I >= 0 && hv_PartType.I < minEBRs.Count ? hv_PartType.I : 0;
                    resultDetails.EBRDetections.Add(new EBRDetectionResultDetail(hv_Id.I, hv_EBR.D, hv_EBR.D, 0, 0, minEBRs[idx]));
                    if (MinT.D >= 0)
                    {
                        resultDetails.BWDetections.Add(new BWDetectionResultDetail(hv_Id.I, MinT.D, 0, 0));
                    }
                    //if (BubbleArea.D > 0)
                    //{
                    //    resultDetails.VoidDetections.Add(new VoidDetectionResultDetail(hv_Id.I, BubbleArea.D, 0, 0));
                    //}

                    if (Type == "EBROoLSL")
                    {
                        labelNames[names[idx]] = "";

                        ngDetails.Add(new NGResultAreaDetail()
                        {
                            BBoxId = ngDetails.Count,
                            NGName = names[idx],
                            Area = ebr,
                            Width = 1,
                            Height = ebr,
                            Row = FontRow.D,
                            Col = FontCol.D,
                        });
                    }

                    //if (minEBRs.Count > idx)
                    //{
                    //    if (ebr < minEBRs[idx])
                    //    {
                    //        labelNames[names[idx]] = "";

                    //        ngDetails.Add(new NGResultAreaDetail()
                    //        {
                    //            BBoxId = ngDetails.Count,
                    //            NGName = names[idx],
                    //            Area = ebr,
                    //            Width = 1,
                    //            Height = ebr,
                    //            Row = FontRow.D,
                    //            Col = FontCol.D,
                    //        });
                    //    }
                    //}

                }
            }



        }

        // 分析出EBR缺陷、序列化
        public static void ParseClassicalVisionNG(HTuple hv_DrawDictAry, string ngName, Dictionary<string, string> labelNames, List<NGResultAreaDetail> ngDetails)
        {
            // BW, EBR  判断与序列化
            // sEBRContent1, sEBRContent2
            if (hv_DrawDictAry != null)
            {

                for (int i = 0; i < hv_DrawDictAry.Length; i++)
                {
                    var hv_DictHandle = hv_DrawDictAry[i];
                    HOperatorSet.GetDictTuple(hv_DictHandle, "Type", out var hv_Type);
                    HOperatorSet.GetDictTuple(hv_DictHandle, "Area", out var hv_Area);
                    HOperatorSet.GetDictTuple(hv_DictHandle, "Row", out var hv_Row);
                    HOperatorSet.GetDictTuple(hv_DictHandle, "Column", out var hv_Column);
                    HOperatorSet.GetDictTuple(hv_DictHandle, "Width", out var hv_Width);
                    HOperatorSet.GetDictTuple(hv_DictHandle, "Height", out var hv_Height);
                    HOperatorSet.GetDictTuple(hv_DictHandle, "Value", out var hv_Value);

                    if (hv_Type != null && hv_Type.S == ngName)
                    {
                        labelNames[ngName] = "";

                        ngDetails.Add(new NGResultAreaDetail()
                        {
                            BBoxId = ngDetails.Count,
                            NGName = ngName,
                            Area = hv_Value.D, // value 是实际缺陷的度量值
                            Width = hv_Width.D,
                            Height = hv_Height.D,
                            Row = hv_Row.D,
                            Col = hv_Column.D,
                        });
                    }
                }

            }
        }

        // 连接传统视觉NG区域
        public static void ConcatClassicalVisionNGRegion(HTuple hv_DrawDictAry, HObject ho_NGRegion, string[] ngNames)
        {
            if (hv_DrawDictAry == null || ho_NGRegion == null)
            {
                return;
            }
            for (int idx = 0; idx < hv_DrawDictAry.Length; idx++)
            {
                var hv_DictHandle = hv_DrawDictAry[idx];
                HOperatorSet.GetDictTuple(hv_DictHandle, "Type", out var hv_Type);

                if (hv_Type != null && ngNames.Contains(hv_Type.S))
                {
                    HOperatorSet.GetDictObject(out var ho_Region, hv_DictHandle, "Region");
                    HOperatorSet.ConcatObj(ho_NGRegion, ho_Region, out ho_NGRegion);
                }
            }
        }

        public static void ConcatClassicalVisionDisplayText(HTuple hv_TextDictAry, List<DispTextData> dispTextDatas, int offsetRow)
        {
            if (hv_TextDictAry == null || dispTextDatas == null)
            {
                return;
            }
            for (int i = 0; i < hv_TextDictAry.Length; i++)
            {
                var hv_DictHandle = hv_TextDictAry[i];
                HOperatorSet.GetDictTuple(hv_DictHandle, "Text", out var hv_Text); // 绘制的文件
                HOperatorSet.GetDictTuple(hv_DictHandle, "Color", out var hv_Color); // 绘制的颜色
                HOperatorSet.GetDictTuple(hv_DictHandle, "Row", out var hv_Row); // 绘制的行
                HOperatorSet.GetDictTuple(hv_DictHandle, "Column", out var hv_Column); // 绘制的列

                dispTextDatas.Add(new DispTextData
                {
                    Text = hv_Text.S,
                    Color = hv_Color.S,
                    Row = hv_Row.D - offsetRow,
                    Col = hv_Column.D
                });
            }
        }

        public static void DisposeDetectionPaintCache(HObject ho_BrazingBorder, HTuple hv_FindShapeDict, HTuple hv_DrawDictAry, HTuple hv_DictAryC)
        {
            try
            {
                if (hv_FindShapeDict != null)
                {
                    //for (int j = 0; j <= hv_FindShapeDict.Length - 1; j++)
                    //{
                        //var hv_Dict = hv_FindShapeDict[j];
                        //HOperatorSet.GetDictObject(out var ho_ShapeModel, hv_Dict, "ShapeModel");
                        //HOperatorSet.GetDictObject(out var ho_ChRegionL, hv_Dict, "ChRegionL");
                        // 释放
                        //ho_ShapeModel.Dispose();
                        //ho_ChRegionL.Dispose();
                    //}
                }

                // 绘画对象集A
                DisposeClassicalVisionDrawDictAry(hv_DrawDictAry);

                // AI
                if (hv_DictAryC != null)
                {
                    for (int Index2 = 0; Index2 <= hv_DictAryC.Length - 1; Index2++)
                    {
                        var hv_Dict = hv_DictAryC[Index2];
                        HOperatorSet.GetDictObject(out var ho_OverflowBox, hv_Dict, "BBox");
                        ho_OverflowBox.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                // 打印异常消息
                LogHelper.Error($"视觉 Main处理 合成结果图失败 DisposeDetectionPaintCache：{ex.Message}");

                // 打印堆栈跟踪
                LogHelper.Error($"堆栈跟踪：{ex.StackTrace}");

                // 如果有内部异常，也打印出来
                if (ex.InnerException != null)
                {
                    LogHelper.Error($"内部异常：{ex.InnerException.Message}");
                    LogHelper.Error($"内部异常堆栈跟踪：{ex.InnerException.StackTrace}");
                }
            }
            finally
            {
                // 释放资源
                ho_BrazingBorder?.Dispose();
                hv_FindShapeDict?.Dispose();
                // 断胶A
                hv_DrawDictAry?.Dispose();
                // AI结果
                hv_DictAryC?.Dispose();
            }
        }

        /// <summary>
        /// 确保 dict 已初始化为 Halcon dict
        /// </summary>
        public static void EnsureDict(ref HTuple dict)
        {
            if (dict != null)
                return;

            HOperatorSet.CreateDict(out dict);
        }

        /// <summary>
        /// 将参数写入 Halcon dict
        /// - typed 优先
        /// - raw 只补充 typed 不存在的 key
        /// </summary>
        public static void MergeParamsIntoDict(
            HTuple dict,
            Dictionary<string, object> typed,
            Dictionary<string, string> raw)
        {
            if (dict == null)
                throw new ArgumentException("dict is null or not initialized", nameof(dict));

            // 1) typed
            if (typed != null && typed.Count > 0)
            {
                foreach (var kv in typed)
                {
                    var key = NormalizeKey(kv.Key);
                    if (string.IsNullOrEmpty(key)) continue;

                    var tuple = ToHTupleSafe(kv.Value);
                    HOperatorSet.SetDictTuple(dict, key, tuple);
                }
            }

            // 2) raw 补充（不覆盖 typed）
            if (raw != null && raw.Count > 0)
            {
                foreach (var kv in raw)
                {
                    var key = NormalizeKey(kv.Key);
                    if (string.IsNullOrEmpty(key)) continue;

                    if (typed != null && typed.ContainsKey(key))
                        continue;

                    HOperatorSet.SetDictTuple(dict, key, kv.Value ?? string.Empty);
                }
            }
        }

        private static string NormalizeKey(string k)
        {
            return string.IsNullOrWhiteSpace(k) ? string.Empty : k.Trim();
        }

        /// <summary>
        /// 把常见 C# 类型转成 Halcon HTuple（可标量可向量）
        /// 支持：
        /// - int/uint/short/ushort/long/double/float/bool/string
        /// - IEnumerable&lt;int/double/string/bool&gt;
        /// - 数组 int[]/double[]/string[]
        /// 其他类型：ToString 兜底
        /// </summary>
        private static HTuple ToHTupleSafe(object value)
        {
            if (value == null) return new HTuple("");

            // string
            if (value is string s)
                return new HTuple(s);

            // bool
            if (value is bool b)
                return new HTuple(b ? "true" : "false"); // Halcon里用字符串也稳

            // integer types
            if (value is int i) return new HTuple(i);
            if (value is short i16) return new HTuple((int)i16);
            if (value is long i64) return new HTuple(i64);
            if (value is uint ui) return new HTuple((long)ui);
            if (value is ushort ui16) return new HTuple((int)ui16);
            if (value is ulong ui64) return new HTuple((double)ui64); // 兜底

            // float/double
            if (value is double d) return new HTuple(d);
            if (value is float f) return new HTuple((double)f);
            if (value is decimal dec) return new HTuple((double)dec);

            // arrays
            if (value is int[] ia) return new HTuple(ia);
            if (value is double[] da) return new HTuple(da);
            if (value is string[] sa) return new HTuple(sa);

            // IEnumerable (list)
            if (value is IEnumerable<int> ienum)
                return new HTuple(ienum.ToArray());
            if (value is IEnumerable<double> denum)
                return new HTuple(denum.ToArray());
            if (value is IEnumerable<string> senum)
                return new HTuple(senum.ToArray());
            if (value is IEnumerable<bool> benum)
                return new HTuple(benum.Select(x => x ? "true" : "false").ToArray());

            // 兜底：ToString
            return new HTuple(value.ToString());
        }

    }

}
