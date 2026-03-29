using System;

namespace MyTestFramework.Exceptions
{
    public class AssertFailedException : Exception
    {
        public AssertFailedException(string message) : base(message) { }
    }

    public class TestTimeoutException : Exception
    {
        public TestTimeoutException(string message) : base(message) { }
    }
}