using System;
using System.Threading.Tasks;
using MyTestFramework;
using MyTestFramework.Attributes;
using MyTestFramework.Context;
using CalculatorApp;
using System.Collections.Generic;

namespace CalculatorApp.Tests
{
    public class DbFixture : IDisposable
    {
        public DatabaseService Db { get; }
        public Guid Id { get; } = Guid.NewGuid();

        public DbFixture()
        {
            Db = new DatabaseService();
            Db.Connect();
        }

        public void Dispose()
        {
            Db.Disconnect();
        }
    }

    [TestClass]
    public class CalculatorTests : ISharedContext<DbFixture>
    {
        private Calculator _calculator;
        private DbFixture _fixture;

        public void SetContext(DbFixture context)
        {
            _fixture = context;
        }

        [Setup]
        public void Init()
        {
            _calculator = new Calculator();
        }

        [Teardown]
        public void Cleanup()
        {
            _calculator = null;
        }

        [TestMethod("Simple addition test")]
        public void Add_ShouldReturnCorrectSum()
        {
            int result = _calculator.Add(2, 3);
            Assert.AreEqual(5, result); 
            Assert.IsTrue(result > 0);  
            _fixture.Db.SaveOperation("Add");
        }

        [TestMethod]
        [TestCase(10, 5, 5)]
        [TestCase(0, 0, 0)]
        [TestCase(-5, -5, 0)]
        public void Subtract_Parameterized(int a, int b, int expected)
        {
            int result = _calculator.Subtract(a, b);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Divide_ByZero_ShouldThrowException()
        {
            Assert.Throws<DivideByZeroException>(() => _calculator.Divide(10, 0)); 
        }

        [TestMethod]
        public async Task CalculateAsync_ShouldWork()
        {
            int result = await _calculator.CalculateAsync(4, 6);
            Assert.AreEqual(10, result);
            Assert.AreNotEqual(0, result);
        }

        [TestMethod]
        public void GetGreeting_NullOrEmpty_ReturnsNull()
        {
            string greeting = _calculator.GetGreeting("");
            Assert.IsNull(greeting); 

            greeting = _calculator.GetGreeting("Alice");
            Assert.IsNotNull(greeting); 
            Assert.IsFalse(greeting == "Hello, Bob!"); 
        }

        [TestMethod]
        public void CheckDatabaseHistory()
        {
            Assert.IsNotNull(_fixture);
            Assert.IsTrue(_fixture.Db.IsConnected);
            
            _fixture.Db.SaveOperation("TestOp");
            
            Assert.Contains("TestOp", _fixture.Db.History); 
            Assert.DoesNotContain("NonExistentOp", _fixture.Db.History); 
        }

        [TestMethod]
        public async Task AsyncThrows_Test()
        {
            Func<Task> act = async () => 
            {
                await Task.Delay(10);
                throw new InvalidOperationException("Async error");
            };
            
            await Assert.ThrowsAsync<InvalidOperationException>(act);
        }
        
        [TestMethod(Ignore = true)]
        public void IgnoredTest()
        {
            Assert.IsTrue(false, "This should not run");
        }
    }
}
