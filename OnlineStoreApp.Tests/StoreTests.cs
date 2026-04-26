using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyTestFramework;
using MyTestFramework.Attributes;
using MyTestFramework.Context;
using OnlineStoreApp.Exceptions;
using OnlineStoreApp.Models;
using OnlineStoreApp.Services;

namespace OnlineStoreApp.Tests
{
    public class SharedDbFixture : IDisposable
    {
        public ConcurrentBag<string> TestLogs { get; } = new ConcurrentBag<string>();
        public SharedDbFixture() { TestLogs.Add("Database Connected."); }
        public void Log(string message) { TestLogs.Add(message); }
        public void Dispose() { TestLogs.Clear(); }
    }
    [TestClass]
    public class OrderServiceTests : ISharedContext<SharedDbFixture>
    {
        private InventoryService _inventory;
        private PaymentProcessor _paymentProcessor;
        private OrderService _orderService;
        private SharedDbFixture _db;

        public void SetContext(SharedDbFixture context)
        {
            _db = context;
        }

        [Setup]
        public void Setup()
        {
            _inventory = new InventoryService();
            _inventory.AddProduct(new Product { Id = 1, Name = "Laptop", Price = 1500.00m, StockQuantity = 5 });
            _inventory.AddProduct(new Product { Id = 2, Name = "Mouse", Price = 25.50m, StockQuantity = 100 });
            _inventory.AddProduct(new Product { Id = 3, Name = "Monitor", Price = 300.00m, StockQuantity = 0 });

            _paymentProcessor = new PaymentProcessor();
            _orderService = new OrderService(_inventory, _paymentProcessor);
        }

        [Teardown]
        public void Cleanup()
        {
            _inventory = null;
            _paymentProcessor = null;
            _orderService = null;
        }

        [TestMethod("Магия Дерева Выражений (Тест намеренно упадет!)")]
        public async Task ExpressionTree_MagicTest_Fails()
        {
            await Task.Delay(500); 

            int myCartTotal = 1500;
            int accountBalance = 1000;
            
            Assert.Check(() => myCartTotal + 500 == accountBalance * 2); 
            Assert.Check(() => myCartTotal == accountBalance); 
        }

        public static IEnumerable<object[]> GetCardsFromDatabase()
        {
            yield return new object[] { "4111222233334444", true }; 
            yield return new object[] { "5111222233334444", false }; 
        }

        [TestMethod("Проверка платежей через источник данных (yield return)")][TestCaseSource(nameof(GetCardsFromDatabase))]
        public async Task ProcessPayment_UsingYieldReturn(string cardNum, bool expectedResult)
        {
            await Task.Delay(500);

            var result = await _paymentProcessor.ProcessPaymentAsync(100, cardNum);
            Assert.AreEqual(expectedResult, result);
        }

        [TestMethod("Этот тест не запустится из-за делегата-фильтрации")]
        [Category("Manual")] 
        public void ShouldBeFilteredOut()
        {
            Assert.IsTrue(false, "ЕСЛИ ВЫ ВИДИТЕ ЭТО, ФИЛЬТРАЦИЯ НЕ СРАБОТАЛА!");
        }

        [TestMethod("Successfully checkout a valid order")]
        [Priority("High")]
        public async Task CheckoutAsync_ValidOrder_Success()
        {
            var cart = new List<Product> { _inventory.GetProduct(1) };
            var order = await _orderService.CheckoutAsync(cart, "4111222233334444");
            Assert.IsNotNull(order);
        }

        [TestMethod][TestCase("111122223333")]
        [TestCase("")]
        public async Task ProcessPayment_InvalidCard_ThrowsPaymentFailed(string cardStr)
        {
            Func<Task> act = async () => await _paymentProcessor.ProcessPaymentAsync(100, cardStr);
            await Assert.ThrowsAsync<PaymentFailedException>(act);
        }
    }
}