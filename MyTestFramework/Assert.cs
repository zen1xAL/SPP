using System;
using System.Collections;
using System.Linq.Expressions; 
using System.Threading.Tasks;
using MyTestFramework.Exceptions;

namespace MyTestFramework
{
    public static class Assert
    {
        public static void Check(Expression<Func<bool>> expression)
        {
            var compiledDelegate = expression.Compile();
            if (compiledDelegate.Invoke()) return;

            string detailedMessage = "Сбой Assert.Check!\n";
            detailedMessage += $"Структура: {expression.Body}\n";

            if (expression.Body is BinaryExpression binaryExpr)
            {
                var leftValue = Expression.Lambda(binaryExpr.Left).Compile().DynamicInvoke();
                var rightValue = Expression.Lambda(binaryExpr.Right).Compile().DynamicInvoke();
                string operation = binaryExpr.NodeType.ToString();

                detailedMessage += $"Оператор:  {operation}\n" +
                                   $"Операнд 1 (Слева):  {binaryExpr.Left} => фактическое значение:[{leftValue ?? "null"}]\n" +
                                   $"Операнд 2 (Справа): {binaryExpr.Right} => фактическое значение: [{rightValue ?? "null"}]";
            }
            else
            {
                detailedMessage += "Выражение не является бинарным сравнением.";
            }

            throw new AssertFailedException(detailedMessage);
        }

        public static void IsTrue(bool condition, string message = "Expected true but was false.")
        {
            if (!condition) throw new AssertFailedException(message);
        }

        public static void IsFalse(bool condition, string message = "Expected false but was true.")
        {
            if (condition) throw new AssertFailedException(message);
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!Equals(expected, actual))
                throw new AssertFailedException(message ?? $"Expected <{expected}> but was <{actual}>.");
        }

        public static void AreNotEqual(object notExpected, object actual, string message = null)
        {
            if (Equals(notExpected, actual))
                throw new AssertFailedException(message ?? $"Expected any value except <{notExpected}>, but was <{actual}>.");
        }

        public static void IsNull(object obj, string message = "Expected null but was not null.")
        {
            if (obj != null) throw new AssertFailedException(message);
        }

        public static void IsNotNull(object obj, string message = "Expected not null but was null.")
        {
            if (obj == null) throw new AssertFailedException(message);
        }

        public static T Throws<T>(Action action, string message = null) where T : Exception
        {
            try { action(); }
            catch (T ex) { return ex; }
            catch (Exception ex) { throw new AssertFailedException(message ?? $"Expected {typeof(T).Name}, but {ex.GetType().Name} was thrown."); }
            throw new AssertFailedException(message ?? $"Expected exception {typeof(T).Name}, but none was thrown.");
        }

        public static async Task<T> ThrowsAsync<T>(Func<Task> action, string message = null) where T : Exception
        {
            try { await action(); }
            catch (T ex) { return ex; }
            catch (Exception ex) { throw new AssertFailedException(message ?? $"Expected {typeof(T).Name}, but {ex.GetType().Name} was thrown."); }
            throw new AssertFailedException(message ?? $"Expected exception {typeof(T).Name}, but none was thrown.");
        }

        public static void Contains(object expected, IEnumerable collection, string message = null)
        {
            foreach (var item in collection) if (Equals(expected, item)) return;
            throw new AssertFailedException(message ?? $"Expected collection to contain <{expected}>.");
        }

        public static void DoesNotContain(object notExpected, IEnumerable collection, string message = null)
        {
            foreach (var item in collection) if (Equals(notExpected, item)) throw new AssertFailedException(message ?? $"Expected collection to NOT contain <{notExpected}>.");
        }
    }
}