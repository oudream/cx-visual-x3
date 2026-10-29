using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CxVisual.Professional.Vision
{
    /// <summary>
    /// 将已提交的PVC单次Run投影为可复核CSV。只输出数值/几何元数据和稳定身份，
    /// Image/Mask字节仍由FIM原像素导出负责；不伪造磁盘Artifact引用。
    /// </summary>
    public static class CxVisionRunCsvExporter
    {
        private const int MaxRows = 20000;
        private static readonly string[] Headers = {
            "schema_version","run_id","source_id","completed_utc","config_revision",
            "plan_fingerprint","capability_version","image_width","image_height",
            "item_id","output_port","kind","name","value","unit",
            "region_min_x","region_min_y","region_max_x","region_max_y",
            "image_format","result_width","result_height","contour_is_hole",
            "contour_points"
        };

        public static void Write(TextWriter writer, CxVisionRunSummary run,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (writer == null) throw new ArgumentNullException("writer");
            if (run == null) throw new ArgumentNullException("run");
            cancellation.ThrowIfCancellationRequested();
            if (run.RunId == Guid.Empty || string.IsNullOrWhiteSpace(run.SourceId) ||
                run.Width <= 0 || run.Height <= 0 || run.Items == null ||
                run.Items.Count > MaxRows)
                throw new InvalidOperationException("vision.csv.invalid_run");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CxVisionResultItem item in run.Items)
            {
                cancellation.ThrowIfCancellationRequested();
                if (item == null || item.RunId != run.RunId ||
                    item.SourceId != run.SourceId ||
                    string.IsNullOrWhiteSpace(item.ItemId) || !ids.Add(item.ItemId))
                    throw new InvalidOperationException("vision.csv.item_identity_mismatch");
                if ((item.Kind == CxVisionResultKind.Contour && item.Contour == null) ||
                    ((item.Kind == CxVisionResultKind.Image ||
                      item.Kind == CxVisionResultKind.Mask) && item.Image == null))
                    throw new InvalidOperationException("vision.csv.missing_typed_value");
            }
            writer.WriteLine(string.Join(",", Headers));
            foreach (CxVisionResultItem item in run.Items)
            {
                cancellation.ThrowIfCancellationRequested();
                string value = item.IntegerValue.HasValue ?
                    item.IntegerValue.Value.ToString(CultureInfo.InvariantCulture) :
                    item.BooleanValue.HasValue ?
                    item.BooleanValue.Value.ToString().ToLowerInvariant() :
                    item.Region != null ?
                    item.Region.PixelArea.ToString(CultureInfo.InvariantCulture) :
                    item.Contour != null ?
                    item.Contour.SignedAreaPixels.ToString("R", CultureInfo.InvariantCulture) : "";
                string[] fields = {
                    "vision.run.csv.v1", run.RunId.ToString("N"), Protect(run.SourceId),
                    run.CompletedUtc.HasValue ?
                        run.CompletedUtc.Value.ToString("O", CultureInfo.InvariantCulture) : "",
                    run.ConfigRevision.ToString(CultureInfo.InvariantCulture),
                    Protect(run.PlanFingerprint), Protect(run.CapabilityVersion),
                    run.Width.ToString(CultureInfo.InvariantCulture),
                    run.Height.ToString(CultureInfo.InvariantCulture),
                    Protect(item.ItemId), Protect(item.OutputPort),
                    item.Kind.ToString(), Protect(item.Name),
                    value, Protect(item.Unit),
                    item.Region == null ? "" :
                        item.Region.MinX.ToString(CultureInfo.InvariantCulture),
                    item.Region == null ? "" :
                        item.Region.MinY.ToString(CultureInfo.InvariantCulture),
                    item.Region == null ? "" :
                        item.Region.MaxX.ToString(CultureInfo.InvariantCulture),
                    item.Region == null ? "" :
                        item.Region.MaxY.ToString(CultureInfo.InvariantCulture),
                    item.Image == null ? "" : item.Image.PixelFormat.ToString(),
                    item.Image == null ? "" :
                        item.Image.Width.ToString(CultureInfo.InvariantCulture),
                    item.Image == null ? "" :
                        item.Image.Height.ToString(CultureInfo.InvariantCulture),
                    item.Contour == null ? "" :
                        (item.Contour.IsHole ? "true" : "false"),
                    item.Contour == null ? "" :
                        item.Contour.Points.Count.ToString(CultureInfo.InvariantCulture)
                };
                writer.WriteLine(string.Join(",", fields.Select(Escape)));
            }
        }

        // CSV引号之外，避免来自设备/图像来源的自由字符串在表格软件里成为公式。
        private static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string trimmed = value.TrimStart(' ', '\t', '\r', '\n');
            if (trimmed.Length > 0 &&
                (trimmed[0] == '=' || trimmed[0] == '+' ||
                 trimmed[0] == '-' || trimmed[0] == '@'))
                return "'" + value;
            return value;
        }

        private static string Escape(string value)
        {
            string text = value ?? "";
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        public static void SaveFile(string path, CxVisionRunSummary run,
            bool overwrite = false,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("文件路径不能为空", "path");
            if (run == null) throw new ArgumentNullException("run");
            cancellation.ThrowIfCancellationRequested();
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("结果目录不存在：" + directory);
            if (!overwrite && File.Exists(full))
                throw new IOException("结果CSV已存在且未允许覆盖");
            string temporary = Path.Combine(directory, "." + Path.GetFileName(full) +
                "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream,
                        new UTF8Encoding(false), 4096, true))
                    {
                        Write(writer, run, cancellation);
                        writer.Flush();
                    }
                    stream.Flush(true);
                }
                cancellation.ThrowIfCancellationRequested();
                if (overwrite && File.Exists(full))
                    File.Replace(temporary, full, null);
                else
                    File.Move(temporary, full);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
