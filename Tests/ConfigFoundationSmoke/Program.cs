using System;
using CxVisual.Tests.ArchitectureSmoke;

namespace CxVisual.Tests.ConfigFoundationSmoke
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                int checks = ConfigurationIntegrationTests.RunAll();
                ConfigV13ExtensionTests.Run();
                ConfigArchitectureCompletionTests.Run();
                ConfigDeveloperExperienceTests.Run();
                Console.WriteLine("PASS 统一配置底座独立验收; 基础断言数=" + checks);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL 统一配置底座独立验收: " + ex);
                return 1;
            }
        }
    }
}
