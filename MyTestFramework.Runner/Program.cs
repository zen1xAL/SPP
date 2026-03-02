using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MyTestFramework.Attributes;
using MyTestFramework.Exceptions;

namespace MyTestFramework.Runner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: runner <path-to-test-dll>");
                return;
            }

            string dllPath = Path.GetFullPath(args[0]);
            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"Error: File '{dllPath}' not found.");
                return;
            }

            Console.WriteLine($"Loading assembly: {dllPath}");
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(dllPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load assembly: {ex.Message}");
                return;
            }

            int totalPassed = 0;
            int totalFailed = 0;
            int totalIgnored = 0;

            var testClasses = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TestClassAttribute>() != null && !t.IsAbstract)
                .ToList();

            Console.WriteLine($"Found {testClasses.Count} test class(es).");

            foreach (var testClass in testClasses)
            {
                Console.WriteLine($"\nRunning tests in {testClass.Name}...");

                // Shared context resolution
                object sharedContextInstance = null;
                var sharedContextInterface = testClass.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == "ISharedContext`1");

                if (sharedContextInterface != null)
                {
                    var contextType = sharedContextInterface.GetGenericArguments()[0];
                    sharedContextInstance = Activator.CreateInstance(contextType);
                }

                var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                var setupMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);
                var teardownMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<TeardownAttribute>() != null);
                var testMethods = methods.Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null).ToList();

                foreach (var testMethod in testMethods)
                {
                    var testAttr = testMethod.GetCustomAttribute<TestMethodAttribute>();
                    string testName = !string.IsNullOrEmpty(testAttr.Description) ? testAttr.Description : testMethod.Name;

                    if (testAttr.Ignore)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  [IGNORED] {testName}");
                        Console.ResetColor();
                        totalIgnored++;
                        continue;
                    }

                    var testCases = testMethod.GetCustomAttributes<TestCaseAttribute>().ToList();
                    if (testCases.Any())
                    {
                        foreach (var testCase in testCases)
                        {
                            string caseName = $"{testName}({string.Join(", ", testCase.Parameters)})";
                            var result = await RunTestAsync(testClass, testMethod, setupMethod, teardownMethod, sharedContextInstance, testCase.Parameters, caseName);
                            if (result.Passed) totalPassed++;
                            if (result.Failed) totalFailed++;
                        }
                    }
                    else
                    {
                        var result = await RunTestAsync(testClass, testMethod, setupMethod, teardownMethod, sharedContextInstance, null, testName);
                        if (result.Passed) totalPassed++;
                        if (result.Failed) totalFailed++;
                    }
                }

                // Clean up shared context if it's IDisposable
                if (sharedContextInstance is IDisposable disposableContext)
                {
                    disposableContext.Dispose();
                }
            }

            Console.WriteLine("\n==============================");
            Console.WriteLine($"Total tests: {totalPassed + totalFailed + totalIgnored}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Passed:  {totalPassed}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed:  {totalFailed}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Ignored: {totalIgnored}");
            Console.ResetColor();
            Console.WriteLine("==============================\n");
        }

        private static async Task<(bool Passed, bool Failed)> RunTestAsync(
            Type testClass, MethodInfo testMethod, MethodInfo setupMethod, MethodInfo teardownMethod,
            object sharedContextInstance, object[] parameters, string testName)
        {
            object classInstance = Activator.CreateInstance(testClass);
            bool passed = false;
            bool failed = false;

            if (sharedContextInstance != null)
            {
                var method = testClass.GetMethod("SetContext");
                if (method != null)
                {
                    method.Invoke(classInstance, new[] { sharedContextInstance });
                }
            }

            try
            {
                setupMethod?.Invoke(classInstance, null);

                if (testMethod.ReturnType == typeof(Task) || (testMethod.ReturnType.IsGenericType && testMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                {
                    var task = (Task)testMethod.Invoke(classInstance, parameters);
                    await task.ConfigureAwait(false);
                }
                else
                {
                    testMethod.Invoke(classInstance, parameters);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [PASSED]  {testName}");
                Console.ResetColor();
                passed = true;
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException;
                if (inner is AssertFailedException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [FAILED]  {testName}");
                    Console.WriteLine($"    -> {inner.Message}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [ERROR]   {testName}");
                    Console.WriteLine($"    -> Unhandled exception: {inner?.GetType().Name}: {inner?.Message}");
                    Console.ResetColor();
                }
                failed = true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [ERROR]   {testName}");
                Console.WriteLine($"    -> {ex.Message}");
                Console.ResetColor();
                failed = true;
            }
            finally
            {
                try
                {
                    teardownMethod?.Invoke(classInstance, null);
                }
                catch (Exception teardownEx)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [ERROR] Teardown failed: {teardownEx.InnerException?.Message ?? teardownEx.Message}");
                    Console.ResetColor();
                }
            }
            
            return (passed, failed);
        }
    }
}
