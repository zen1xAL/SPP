using System;
using System.Collections;
using System.Threading.Tasks;
using MyTestFramework.Exceptions;

namespace MyTestFramework
{
    public static class Assert
    {
        public static void IsTrue(bool condition, string message = "Expected true but was false.")
        {
            if (!condition)
            {
                throw new AssertFailedException(message);
            }
        }

        public static void IsFalse(bool condition, string message = "Expected false but was true.")
        {
            if (condition)
            {
                throw new AssertFailedException(message);
            }
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!Equals(expected, actual))
            {
                string msg = message ?? $"Expected <{expected}> but was <{actual}>.";
                throw new AssertFailedException(msg);
            }
        }

        public static void AreNotEqual(object notExpected, object actual, string message = null)
        {
            if (Equals(notExpected, actual))
            {
                string msg = message ?? $"Expected any value except <{notExpected}>, but was <{actual}>.";
                throw new AssertFailedException(msg);
            }
        }

        public static void IsNull(object obj, string message = "Expected null but was not null.")
        {
            if (obj != null)
            {
                throw new AssertFailedException(message);
            }
        }

        public static void IsNotNull(object obj, string message = "Expected not null but was null.")
        {
            if (obj == null)
            {
                throw new AssertFailedException(message);
            }
        }

        public static T Throws<T>(Action action, string message = null) where T : Exception
        {
            try
            {
                action();
            }
            catch (T ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new AssertFailedException(message ?? $"Expected exception of type {typeof(T).Name}, but {ex.GetType().Name} was thrown.");
            }
            
            throw new AssertFailedException(message ?? $"Expected exception of type {typeof(T).Name}, but no exception was thrown.");
        }

        public static async Task<T> ThrowsAsync<T>(Func<Task> action, string message = null) where T : Exception
        {
            try
            {
                await action();
            }
            catch (T ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new AssertFailedException(message ?? $"Expected exception of type {typeof(T).Name}, but {ex.GetType().Name} was thrown.");
            }
            
            throw new AssertFailedException(message ?? $"Expected exception of type {typeof(T).Name}, but no exception was thrown.");
        }

        public static void Contains(object expected, IEnumerable collection, string message = null)
        {
            foreach (var item in collection)
            {
                if (Equals(expected, item))
                {
                    return;
                }
            }
            throw new AssertFailedException(message ?? $"Expected collection to contain <{expected}>.");
        }

        public static void DoesNotContain(object notExpected, IEnumerable collection, string message = null)
        {
            foreach (var item in collection)
            {
                if (Equals(notExpected, item))
                {
                    throw new AssertFailedException(message ?? $"Expected collection to NOT contain <{notExpected}>.");
                }
            }
        }
    }
}
