using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.Discovery
{
    public sealed class TCTReflectionTestDiscoverer : ITCTTestDiscoverer
    {
        private readonly Assembly[] _assemblies;
        public TCTReflectionTestDiscoverer(params Assembly[] assemblies)
        {
            _assemblies = assemblies ?? new Assembly[0];
        }
        public string Id { get { return "TCT.Reflection"; } }
        public IEnumerable<ITCTTestCase> Discover()
        {
            foreach (Assembly assembly in _assemblies)
            {
                if (assembly == null) continue;
                IEnumerable<Type> types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex)
                {
                    throw new InvalidOperationException("Cannot discover " + assembly.FullName, ex);
                }
                foreach (Type type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
                {
                    if (!typeof(ITCTTestCase).IsAssignableFrom(type) || type.IsAbstract ||
                        type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                    yield return (ITCTTestCase)Activator.CreateInstance(type);
                }
            }
        }
    }
    public sealed class TCTTestDiscoveryService
    {
        public int Register(TCTTestCatalog catalog, IEnumerable<ITCTTestDiscoverer> discoverers)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (discoverers == null) throw new ArgumentNullException("discoverers");
            int count = 0;
            foreach (ITCTTestDiscoverer discoverer in discoverers)
            {
                foreach (ITCTTestCase test in discoverer.Discover())
                {
                    catalog.Register(test);
                    count++;
                }
            }
            return count;
        }
    }
}
