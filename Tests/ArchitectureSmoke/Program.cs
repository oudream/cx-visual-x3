using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args != null && args.Length > 0 && args[0] == "--config-wpf")
                {
                    ConfigWpfL1Tests.Run();
                    return 0;
                }
                UiIndependentAssemblies();
                ConfigConsolidationTests.Run();
                ConfigProfessionalL1Tests.Run();
                ConfigL2L4Tests.Run();
                ConfigV64Cases.Run();
                VisionLiveConfigTests.Run();
                ConfigL1Tests.Run();
                HierarchyTransactionPersistence();
                UndoRedo();
                RejectedCommand();
                Console.WriteLine("PASS architecture tests (UI-free visual editing and contract checks)");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL architecture test: " + ex);
                return 1;
            }
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); }
        private static void UiIndependentAssemblies()
        {
            Assembly[] assemblies = { typeof(CxProjectDocument).Assembly, typeof(CxVisualEditor).Assembly,
                typeof(CxEngineeringSession).Assembly };
            foreach (Assembly assembly in assemblies)
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    Check(reference.Name != "PresentationFramework" && reference.Name != "PresentationCore"
                        && reference.Name != "System.Windows.Forms" && reference.Name != "WindowsBase",
                        "WPF leaked into core: " + assembly.GetName().Name + "/" + reference.Name);
                }
        }
        private static void HierarchyTransactionPersistence()
        {
            CxProjectDocument document = CxDocumentFactory.CreateSample();
            Check(document.Project.Kind == CxNodeKind.Project, "Invalid root");
            CxNode page = document.Project.Children[0];
            Check(page.Kind == CxNodeKind.Page && page.Children[0].Kind == CxNodeKind.Content,
                "Incorrect six-level engineering tree");
            CxGroup group = CxDocumentFactory.FirstGroup(document);
            int before = group.Children.Count;
            CxElement temporary = new CxElement(Guid.NewGuid(), "Transactional", 5, 6, 20, 20);
            CxChangeOperation[] bad = { CxChangeOperation.Add(group.Id, temporary),
                CxChangeOperation.Move(Guid.NewGuid(), 1, 2) };
            CxCommitResult rejected = document.Commit(bad, 0);
            Check(!rejected.Success && document.Revision == 0 && group.Children.Count == before
                && temporary.Parent == null, "Atomic rollback failed");
            CxCommitResult conflict = document.Commit(new[] { CxChangeOperation.Add(group.Id, temporary) }, 5);
            Check(!conflict.Success && conflict.ErrorCode == "REVISION_CONFLICT", "Optimistic lock missing");
            string path = Path.Combine(Path.GetTempPath(), "cxvisual-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                CxDocumentSerializer.Save(document, path);
                CxProjectDocument loaded = CxDocumentSerializer.Load(path);
                Check(loaded.Project.Id == document.Project.Id && loaded.Revision == 0, "XML roundtrip failed");
                Check(CxDocumentFactory.FirstGroup(loaded).Children.Count == before, "XML children missing");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        private static void UndoRedo()
        {
            CxVisualEditor editor = new CxVisualEditor(new CxEngineeringSession(CxDocumentFactory.CreateSample()));
            int count = editor.Scene.Count;
            Check(editor.AddRectangle("New", 10, 20, 30, 40).Success, "Add failed");
            Check(editor.Scene.Count == count + 1 && editor.CanUndo, "Add not recorded");
            Guid id = editor.Scene[editor.Scene.Count - 1].Id;
            Check(editor.Move(id, 90, 100).Success, "Move failed");
            Check(editor.Scene[editor.Scene.Count - 1].Bounds.X == 90, "Projection not updated");
            Check(editor.Undo().Success, "Undo move failed");
            Check(editor.Scene[editor.Scene.Count - 1].Bounds.X == 10, "Undo movement incorrect");
            Check(editor.Undo().Success, "Undo add failed");
            Check(editor.Scene.Count == count, "Undo add missing");
            Check(editor.Redo().Success && editor.Redo().Success, "Redo failed");
            Check(editor.Scene.Count == count + 1, "Redo count incorrect");
            Check(editor.Scene[editor.Scene.Count - 1].Bounds.X == 90, "Redo coordinates incorrect");
            Check(editor.Undo().Success, "Undo before branch failed");
            Check(editor.Move(id, 27, 33).Success, "Branch move failed");
            Check(!editor.CanRedo, "Redo branch should be cleared");
        }
        private static void RejectedCommand()
        {
            CxVisualEditor editor = new CxVisualEditor(new CxEngineeringSession(CxDocumentFactory.CreateSample()));
            CxCommitResult result = editor.Move(Guid.NewGuid(), 20, 30);
            Check(!result.Success, "Unknown element moved");
            Check(editor.Document.Revision == 0, "Rejected command changed revision");
            Check(!editor.CanUndo, "Rejected command entered undo history");
        }
    }
}
