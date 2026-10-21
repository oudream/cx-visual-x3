using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;
using System.Collections.Generic;

namespace CxDeviceSuite.Core
{
     // 编译和加载自定义算子
    public static class DynamicCompiler
    {
        public static (bool success, string log) CompileScripts(IEnumerable<string> scriptFilePaths, string outputAssemblyName)
        {
            List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
            foreach (var filePath in scriptFilePaths)
            {
                string scriptCode = File.ReadAllText(filePath);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(scriptCode));
            }

            var references = new[]
            {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            // 添加其他必要的引用
            // MetadataReference.CreateFromFile(typeof(UtilityClass).Assembly.Location)  // 示例
        };

            var compilation = CSharpCompilation.Create(
                outputAssemblyName,
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);
                if (result.Success)
                {
                    File.WriteAllBytes(outputAssemblyName, ms.ToArray());
                    return (true, "Compilation successful.");
                }
                else
                {
                    var failures = string.Join("\n", GetCompilationErrors(result));
                    return (false, "Compilation failed:\n" + failures);
                }
            }
        }

        private static IEnumerable<string> GetCompilationErrors(EmitResult result)
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    yield return $"{diagnostic.Id}: {diagnostic.GetMessage()}";
                }
            }
        }

        public static Assembly CompileCode(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var assemblyName = Path.GetRandomFileName();
            var references = new MetadataReference[]
            {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                // 添加其他必要的引用
            };

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    // 处理编译错误
                    return null;
                }
                else
                {
                    //File.WriteAllBytes("ScriptAssembly.dll", ms.ToArray());
                }

                ms.Seek(0, SeekOrigin.Begin);
                return Assembly.Load(ms.ToArray());
            }
        }

        private static Type LoadTypeFromAssembly(string assemblyPath, string className)
        {
            var assembly = Assembly.LoadFile(assemblyPath);
            return assembly.GetType(className);
        }

        private static object CreateInstance(string assemblyPath, string className, params object[] constructorArgs)
        {
            var type = LoadTypeFromAssembly(assemblyPath, className);
            return Activator.CreateInstance(type, constructorArgs);
        }

        private static object ExecuteMethod(string assemblyPath, string className, string methodName, object[] constructorArgs, object[] methodArgs)
        {
            var instance = CreateInstance(assemblyPath, className, constructorArgs);
            var method = instance.GetType().GetMethod(methodName);
            return method.Invoke(instance, methodArgs);
        }


        public static object CreateInstance(Assembly assembly, string typeName)
        {
            var type = assembly.GetType(typeName);
            return Activator.CreateInstance(type);
        }
    }

}
