using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MyTestFramework.Attributes;
using MyTestFramework.Exceptions;

namespace MyTestFramework.Runner
{
    class Program
    {
        private static int _totalPassed = 0;
        private static int _totalFailed = 0;
        private static int _totalIgnored = 0;
        private static readonly object ConsoleLock = new object();

        static async Task Main(string[] args)
        {
            if (args.Length == 0) return;

            string dllPath = Path.GetFullPath(args[0]);
            if (!File.Exists(dllPath)) return;

            int maxParallel = Environment.ProcessorCount;
            var maxParallelArg = args.FirstOrDefault(a => a.StartsWith("--parallel="));
            if (maxParallelArg != null) int.TryParse(maxParallelArg.Substring(11), out maxParallel);
            if (args.Contains("--seq")) maxParallel = 1;

            Assembly assembly = Assembly.LoadFrom(dllPath);

            var testClasses = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TestClassAttribute>() != null && !t.IsAbstract)
                .ToList();

            using var semaphore = new SemaphoreSlim(maxParallel);
            var sw = Stopwatch.StartNew();

            var classTasks = testClasses.Select(async testClass =>
            {
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

                var methodTasks = new List<Task>();

                foreach (var testMethod in testMethods)
                {
                    var testAttr = testMethod.GetCustomAttribute<TestMethodAttribute>();
                    string baseName = !string.IsNullOrEmpty(testAttr.Description) ? testAttr.Description : testMethod.Name;

                    if (testAttr.Ignore)
                    {
                        lock (ConsoleLock)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"  [IGNORED] [{testClass.Name}] {baseName}");
                            Console.ResetColor();
                            Interlocked.Increment(ref _totalIgnored);
                        }
                        continue;
                    }

                    var testCases = testMethod.GetCustomAttributes<TestCaseAttribute>().ToList();
                    if (testCases.Any())
                    {
                        foreach (var testCase in testCases)
                        {
                            string caseName = $"[{testClass.Name}] {baseName}({string.Join(", ", testCase.Parameters)})";
                            methodTasks.Add(RunTestAsync(testClass, testMethod, setupMethod, teardownMethod, sharedContextInstance, testCase.Parameters, caseName, semaphore));
                        }
                    }
                    else
                    {
                        string caseName = $"[{testClass.Name}] {baseName}";
                        methodTasks.Add(RunTestAsync(testClass, testMethod, setupMethod, teardownMethod, sharedContextInstance, null, caseName, semaphore));
                    }
                }

                await Task.WhenAll(methodTasks);

                if (sharedContextInstance is IDisposable disposableContext)
                {
                    disposableContext.Dispose();
                }
            });

            await Task.WhenAll(classTasks);
            sw.Stop();

            Console.WriteLine("\n==============================");
            Console.WriteLine($"Time elapsed: {sw.ElapsedMilliseconds} ms (MaxParallel: {maxParallel})");
            Console.WriteLine($"Total tests:  {_totalPassed + _totalFailed + _totalIgnored}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Passed:       {_totalPassed}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed:       {_totalFailed}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Ignored:      {_totalIgnored}");
            Console.ResetColor();
            Console.WriteLine("==============================\n");
        }

        private static async Task RunTestAsync(
            Type testClass, MethodInfo testMethod, MethodInfo setupMethod, MethodInfo teardownMethod,
            object sharedContextInstance, object[] parameters, string testName, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            bool passed = false;
            string errorMsg = null;
            object classInstance = Activator.CreateInstance(testClass);

            try
            {
                if (sharedContextInstance != null)
                {
                    var method = testClass.GetMethod("SetContext");
                    method?.Invoke(classInstance, new[] { sharedContextInstance });
                }

                setupMethod?.Invoke(classInstance, null);

                var timeoutAttr = testMethod.GetCustomAttribute<TimeoutAttribute>();
                int timeoutMs = timeoutAttr?.Milliseconds ?? Timeout.Infinite;

                Task testTask;
                if (testMethod.ReturnType == typeof(Task) || (testMethod.ReturnType.IsGenericType && testMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                {
                    testTask = (Task)testMethod.Invoke(classInstance, parameters);
                }
                else
                {
                    testTask = Task.Run(() => testMethod.Invoke(classInstance, parameters));
                }

                if (timeoutMs != Timeout.Infinite)
                {
                    var delayTask = Task.Delay(timeoutMs);
                    if (await Task.WhenAny(testTask, delayTask) == delayTask)
                    {
                        throw new TestTimeoutException($"Test exceeded timeout of {timeoutMs}ms");
                    }
                }

                await testTask;
                passed = true;
            }
            catch (Exception ex)
            {
                var actual = ex;
                while (actual is TargetInvocationException || actual is AggregateException)
                {
                    actual = actual.InnerException ?? actual;
                    if (actual is AssertFailedException || actual is TestTimeoutException) break;
                }

                errorMsg = actual.Message;
                if (!(actual is AssertFailedException || actual is TestTimeoutException))
                {
                    errorMsg = $"Unhandled exception: {actual.GetType().Name}: {actual.Message}";
                }
            }
            finally
            {
                try
                {
                    teardownMethod?.Invoke(classInstance, null);
                }
                catch (Exception tEx)
                {
                    errorMsg = (errorMsg == null ? "" : errorMsg + " | ") + "Teardown failed: " + (tEx.InnerException?.Message ?? tEx.Message);
                    passed = false;
                }

                lock (ConsoleLock)
                {
                    if (passed)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [PASSED]  {testName}");
                        Interlocked.Increment(ref _totalPassed);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  [FAILED]  {testName}");
                        Console.WriteLine($"    -> {errorMsg}");
                        Interlocked.Increment(ref _totalFailed);
                    }
                    Console.ResetColor();
                }

                semaphore.Release();
            }
        }
    }
}