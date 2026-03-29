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

        public SharedDbFixture()
        {
            TestLogs.Add("Database Connected.");
        }

        public void Log(string message)
        {
            TestLogs.Add(message);
        }

        public void Dispose()
        {
            TestLogs.Clear();
        }
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
            _db.Log("Setting up services...");
            
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
        }[TestMethod("Successfully checkout a valid order")]
        public async Task CheckoutAsync_ValidOrder_Success()
        {
            await Task.Delay(200); 
            var cart = new List<Product> { _inventory.GetProduct(1), _inventory.GetProduct(2) };

            var order = await _orderService.CheckoutAsync(cart, "4111222233334444");

            Assert.IsNotNull(order);
            Assert.IsTrue(order.IsPaid);
            Assert.AreEqual(1525.50m, order.TotalAmount);
            Assert.AreNotEqual(0, order.OrderId);
            Assert.Contains(cart[0], order.Items);
            
            _db.Log("Checkout success test passed.");
        }

        [TestMethod("Empty cart should return null order")]
        public async Task CheckoutAsync_EmptyCart_ReturnsNull()
        {
            var order = await _orderService.CheckoutAsync(new List<Product>(), "4111222233334444");
            Assert.IsNull(order);
            
            var order2 = await _orderService.CheckoutAsync(null, "4111222233334444");
            Assert.IsNull(order2);
        }[TestMethod("Declined payment should throw PaymentFailedException")]
        public async Task CheckoutAsync_DeclinedCard_ThrowsException()
        {
            var cart = new List<Product> { _inventory.GetProduct(1) };
            Func<Task> act = async () => await _orderService.CheckoutAsync(cart, "5111222233334444");
            await Assert.ThrowsAsync<PaymentFailedException>(act);
        }

        [TestMethod("Out of stock product should throw OutOfStockException")]
        public void Checkout_OutOfStock_ThrowsException()
        {
            var product = _inventory.GetProduct(3);
            Assert.Throws<OutOfStockException>(() => _inventory.ReserveProduct(product.Id, 1));
        }

        [TestMethod("Check shared context logs")]
        public void VerifySharedContext()
        {
            Assert.IsTrue(_db.TestLogs.Count > 0);
            
            string unexpectedLog = "Random string not in logs";
            Assert.DoesNotContain(unexpectedLog, _db.TestLogs);
            
            bool containsSetup = false;
            foreach (var log in _db.TestLogs)
            {
                if (log == "Setting up services...") containsSetup = true;
            }
            Assert.IsFalse(!containsSetup);
        }

        [TestMethod][TestCase("111122223333")]
        [TestCase("")]
        public async Task ProcessPayment_InvalidCard_ThrowsPaymentFailed(string cardStr)
        {
            Func<Task> act = async () => await _paymentProcessor.ProcessPaymentAsync(100, cardStr);
            await Assert.ThrowsAsync<PaymentFailedException>(act);
        }

        [TestMethod("Timeout test example")]
        [Timeout(100)]
        public async Task TimeoutTest_Fails()
        {
            await Task.Delay(500); 
        }

        [TestMethod(Ignore = true)]
        public void IncompleteFeature_IgnoredTest()
        {
            Assert.IsTrue(false);
        }
    }
}