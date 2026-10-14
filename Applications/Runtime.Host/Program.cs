using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Lifecycle;
using CxVisual.Mechanism.Visual;
using CxVisual.Foundation.Infrastructure;

namespace CxVisual.Runtime.Host
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string verb = args.Length == 0 ? "demo" : args[0].ToLowerInvariant();
                if (verb == "demo") return Demo();
                if (verb == "save" && args.Length == 2)
                {
                    CxProjectDocument document = CxDocumentFactory.CreateSample();
                    CxDocumentSerializer.Save(document, args[1]);
                    Console.WriteLine("Saved " + Path.GetFullPath(args[1]));
                    return 0;
                }
                if (verb == "load" && args.Length == 2)
                {
                    CxProjectDocument document = CxDocumentSerializer.Load(args[1]);
                    Console.WriteLine("Project=" + document.Project.Name + ", revision=" + document.Revision);
                    Show(CxSceneProjection.Project(document));
                    return 0;
                }
                Console.WriteLine("Usage: CxVisual.Runtime.Host.exe [demo | save <file.xml> | load <file.xml>]");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 1;
            }
        }

        private static int Demo()
        {
            CxProjectDocument document = CxDocumentFactory.CreateSample();
            CxVisualEditor visual = new CxVisualEditor(new CxEngineeringSession(document));
            Console.WriteLine("CxVisual unified engineering demo — no external device or driver");
            using (FLDFileLogSink logger = new FLDFileLogSink(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "runtime-host.log")))
                logger.Write(new FLDLogRecord("RuntimeHost", "Demo", "Engineering demo started"));
            Show(visual.Scene);
            CxCommitResult added = visual.AddRectangle("ROI", 450, 65, 95, 95);
            if (!added.Success) throw new InvalidOperationException(added.ErrorCode);
            CxSceneItem item = visual.Scene[visual.Scene.Count - 1];
            CxCommitResult moved = visual.Move(item.Id, 470, 75);
            CxCommitResult undone = visual.Undo();
            CxCommitResult redone = visual.Redo();
            if (!moved.Success || !undone.Success || !redone.Success)
                throw new InvalidOperationException("Visual edit demo failed");
            Console.WriteLine("Add → Move → Undo → Redo succeeded. Revision=" + document.Revision);
            Show(visual.Scene);
            return 0;
        }
        private static void Show(IList<CxSceneItem> items)
        {
            foreach (CxSceneItem x in items)
                Console.WriteLine("  " + x.Name + " (" + x.Bounds.X + "," + x.Bounds.Y + ") size=" + x.Bounds.Width + "x" + x.Bounds.Height);
        }
    }
}
