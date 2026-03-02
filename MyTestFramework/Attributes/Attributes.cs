using System;

namespace MyTestFramework.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TestClassAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class TestMethodAttribute : Attribute
    {
        public string Description { get; set; }
        public bool Ignore { get; set; }
        
        public TestMethodAttribute() { }
        public TestMethodAttribute(string description) 
        {
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class TestCaseAttribute : Attribute
    {
        public object[] Parameters { get; }

        public TestCaseAttribute(params object[] parameters)
        {
            Parameters = parameters;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class SetupAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class TeardownAttribute : Attribute
    {
    }
}
