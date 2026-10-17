using System;
using System.Threading;
namespace CxVisual.Mechanism.Core
{
    /// <summary>Explicit legacy XML save, shared safe loader and same authoritative session.</summary>
    public static class CxDocumentSerializer
    {
        public static void Save(CxProjectDocument document, string path)
        { if (document == null) throw new ArgumentNullException("document"); document.Session.SaveLegacy(path); }
        public static CxProjectDocument Load(string path) { return MEMFileProjectStore.Load(path); }
    }
}
