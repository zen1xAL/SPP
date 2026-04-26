using System;

namespace MyTestFramework.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TestClassAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class TestMethodAttribute : Attribute
    {
        public string Description { get; set; }
        public bool Ignore { get; set; }

        public TestMethodAttribute() { }
        public TestMethodAttribute(string description) => Description = description;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class TestCaseAttribute : Attribute
    {
        public object[] Parameters { get; }
        public TestCaseAttribute(params object[] parameters) => Parameters = parameters;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class TestCaseSourceAttribute : Attribute
    {
        public string SourceMethodName { get; }
        public TestCaseSourceAttribute(string sourceMethodName) => SourceMethodName = sourceMethodName;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class CategoryAttribute : Attribute
    {
        public string Name { get; }
        public CategoryAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class PriorityAttribute : Attribute
    {
        public string Level { get; }
        public PriorityAttribute(string level) => Level = level;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class SetupAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class TeardownAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class TimeoutAttribute : Attribute
    {
        public int Milliseconds { get; }
        public TimeoutAttribute(int milliseconds) => Milliseconds = milliseconds;
    }
}
