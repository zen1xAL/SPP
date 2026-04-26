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
using MyThreadPool; 

namespace MyTestFramework.Runner
{
    class Program
    {
        private static int _totalPassed = 0;
        private static int _totalFailed = 0;
        private static readonly object ConsoleLock = new object();

        static void Main(string[] args)
        {
            if (args.Length == 0) return;
            string dllPath = Path.GetFullPath(args[0]);
            if (!File.Exists(dllPath)) return;

            Assembly assembly = Assembly.LoadFrom(dllPath);
            var testClasses = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<TestClassAttribute>() != null && !t.IsAbstract)
                .ToList();

            var allTestActions = new List<Action>();

            Func<MethodInfo, bool> testFilter = m => 
            {
                var catAttr = m.GetCustomAttribute<CategoryAttribute>();
                if (catAttr != null && catAttr.Name == "Manual") return false;
                return true;
            };

            foreach (var testClass in testClasses)
            {
                object sharedContextInstance = null;
                var contextInterface = testClass.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().Name == "ISharedContext`1");
                if (contextInterface != null) sharedContextInstance = Activator.CreateInstance(contextInterface.GetGenericArguments()[0]);

                var methods = testClass.GetMethods();
                var setupMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<SetupAttribute>() != null);
                var teardownMethod = methods.FirstOrDefault(m => m.GetCustomAttribute<TeardownAttribute>() != null);
                
                var testMethods = methods.Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null && testFilter(m)).ToList();

                foreach (var testMethod in testMethods)
                {
                    var testAttr = testMethod.GetCustomAttribute<TestMethodAttribute>();
                    if (testAttr.Ignore) continue;

                    string baseName = testAttr.Description ?? testMethod.Name;
                    
                    var testSourceAttr = testMethod.GetCustomAttribute<TestCaseSourceAttribute>();
                    var testCases = testMethod.GetCustomAttributes<TestCaseAttribute>().ToList();

                    if (testSourceAttr != null)
                    {
                        var sourceMethod = testClass.GetMethod(testSourceAttr.SourceMethodName, BindingFlags.Public | BindingFlags.Static);
                        if (sourceMethod != null)
                        {
                            var parametersList = (IEnumerable<object[]>)sourceMethod.Invoke(null, null);
                            foreach (var parameters in parametersList)
                            {
                                string caseName = $"[{testClass.Name}] {baseName}({string.Join(", ", parameters)})";
                                allTestActions.Add(() => RunTestAsync(testClass, testMethod, setupMethod, teardownMethod, sharedContextInstance, parameters, caseName).GetAwaiter().GetResult());
                            }
                        }
                    }
                    else if (testCases.Any())
                    {
                        foreach (var testCase in testCases)
                        {
                            string caseName = $"[{testClass.Name}] {baseName}({string.Join(", ", testCase.Parameters)})";
                            allTestActions.Add(() => RunTestAsync(testClass, testMethod, setupMethod, teardownMethod, sharedContextInstance, testCase.Parameters, caseName).GetAwaiter().GetResult());
                        }
                    }
                    else 
                    {
                        string caseName = $"[{testClass.Name}] {baseName}";
                        allTestActions.Add(() => RunTestAsync(testClass, testMethod, setupMethod, teardownMethod, sharedContextInstance, null, caseName).GetAwaiter().GetResult());
                    }
                }
            }

            Console.WriteLine($"\nВсего отфильтровано тестов к запуску: {allTestActions.Count}");

            using var pool = new CustomThreadPool(minThreads: 2, maxThreads: 10, idleTimeoutMs: 2000);
            
            pool.OnThreadCreated += (msg, active, max) => {
                lock(ConsoleLock) {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[Событие] {msg} (Активно: {active}/{max})");
                    Console.ResetColor();
                }
            };
            pool.OnThreadDestroyed += (msg, active) => {
                lock(ConsoleLock) {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[Событие] {msg} (Активно: {active})");
                    Console.ResetColor();
                }
            };
            pool.OnError += (msg) => {
                lock(ConsoleLock) {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[Событие Ошибки] {msg}");
                    Console.ResetColor();
                }
            };

            using var countdown = new CountdownEvent(allTestActions.Count);
            if (allTestActions.Count == 0) return; 

            var sw = Stopwatch.StartNew();

            Console.WriteLine("\n--- ЗАПУСК ВСЕХ ТЕСТОВ ЧЕРЕЗ ПУЛ ---");
            foreach (var task in allTestActions)
            {
                var capturedTask = task;
                pool.Enqueue(() => { capturedTask(); countdown.Signal(); });
            }

            countdown.Wait();
            sw.Stop();

            Console.WriteLine("\n==============================");
            Console.WriteLine($"Все тесты завершены за {sw.ElapsedMilliseconds} мс");
            Console.WriteLine($"Успешно: {_totalPassed}, Провалено: {_totalFailed}");
            Console.WriteLine("==============================\n");
        }

        private static async Task RunTestAsync(Type testClass, MethodInfo testMethod, MethodInfo setupMethod, MethodInfo teardownMethod, object sharedContextInstance, object[] parameters, string testName)
        {
            bool passed = false;
            string errorMsg = null;
            object classInstance = Activator.CreateInstance(testClass);

            try
            {
                if (sharedContextInstance != null)
                {
                    testClass.GetMethod("SetContext")?.Invoke(classInstance, new[] { sharedContextInstance });
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
            }
            finally
            {
                try { teardownMethod?.Invoke(classInstance, null); } catch { }

                lock (ConsoleLock)
                {
                    if (passed)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [PASSED] {testName}");
                        _totalPassed++;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  [FAILED] {testName}");
                        Console.WriteLine($"    -> {errorMsg}");
                        _totalFailed++;
                    }
                    Console.ResetColor();
                }
            }
        }
    }
}