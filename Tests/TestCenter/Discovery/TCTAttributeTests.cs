using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.Discovery
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TCTTestAttribute : Attribute
    {
        public TCTTestAttribute(string id, string name, TCTTestAspect aspect, string group)
        {
            Id = id; Name = name; Aspect = aspect; Group = group;
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public TCTTestAspect Aspect { get; private set; }
        public string Group { get; private set; }
        public string CapabilityAreaId { get; set; }
        public string CapabilityId { get; set; }
        public string ObjectId { get; set; }
    }

    // 适配“静态方法+测试属性”这一框架形式；NUnit/MSTest须通过独立适配器接入，
    // 不会用字符串伪造其测试结果。
    public sealed class TCTAttributeFrameworkAdapter : ITCTTestFrameworkAdapter
    {
        private readonly Assembly[] _assemblies;
        public TCTAttributeFrameworkAdapter(params Assembly[] assemblies)
        { _assemblies = assemblies ?? new Assembly[0]; }
        public string Id { get { return "TCT.Attribute"; } }
        public IEnumerable<ITCTTestCase> Discover()
        {
            foreach (Assembly assembly in _assemblies)
            {
                if (assembly == null) continue;
                foreach (Type type in assembly.GetTypes().OrderBy(t => t.FullName))
                {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        TCTTestAttribute attribute = method.GetCustomAttributes(typeof(TCTTestAttribute), false)
                            .OfType<TCTTestAttribute>().SingleOrDefault();
                        if (attribute == null) continue;
                        ParameterInfo[] parameters = method.GetParameters();
                        if (method.ReturnType != typeof(void) || parameters.Length != 1 ||
                            parameters[0].ParameterType != typeof(TCTTestContext))
                            throw new InvalidOperationException("Invalid TCTTest method: " + method.DeclaringType.FullName + "." + method.Name);
                        yield return new TCTAttributeTestCase(method, attribute);
                    }
                }
            }
        }
    }

    public sealed class TCTAttributeTestCase : ITCTTestCase
    {
        private readonly MethodInfo _method;
        public TCTAttributeTestCase(MethodInfo method, TCTTestAttribute attribute)
        {
            _method = method;
            Descriptor = new TCTTestCaseDescriptor(attribute.Id, attribute.Name,
                attribute.Aspect, attribute.Group, attribute.CapabilityAreaId,
                attribute.CapabilityId, attribute.ObjectId);
        }
        public TCTTestCaseDescriptor Descriptor { get; private set; }
        public void Execute(TCTTestContext context)
        {
            try { _method.Invoke(null, new object[] { context }); }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
