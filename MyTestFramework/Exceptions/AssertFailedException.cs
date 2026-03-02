using System;

namespace MyTestFramework.Exceptions
{
    public class AssertFailedException : Exception
    {
        public AssertFailedException(string message) : base(message)
        {
        }
    }
}
