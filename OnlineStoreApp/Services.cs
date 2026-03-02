using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OnlineStoreApp.Models;
using OnlineStoreApp.Exceptions;

namespace OnlineStoreApp.Services
{
    public class InventoryService
    {
        private readonly List<Product> _catalog = new List<Product>();

        public void AddProduct(Product product)
        {
            _catalog.Add(product);
        }

        public Product GetProduct(int id)
        {
            return _catalog.FirstOrDefault(p => p.Id == id);
        }

        public void ReserveProduct(int productId, int quantity)
        {
            var product = GetProduct(productId);
            if (product == null) throw new ArgumentException("Product not found");

            if (product.StockQuantity < quantity)
            {
                throw new OutOfStockException($"Not enough items in stock for '{product.Name}'.");
            }

            product.StockQuantity -= quantity;
        }
    }

    public class PaymentProcessor
    {
        public async Task<bool> ProcessPaymentAsync(decimal amount, string cardNumber)
        {
            await Task.Delay(100);

            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.");

            if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 16)
                throw new PaymentFailedException("Invalid card number.");

            return cardNumber.StartsWith("4");
        }
    }

    public class OrderService
    {
        private readonly InventoryService _inventory;
        private readonly PaymentProcessor _payment;

        public OrderService(InventoryService inventory, PaymentProcessor payment)
        {
            _inventory = inventory;
            _payment = payment;
        }

        public async Task<Order> CheckoutAsync(List<Product> cartItems, string cardNumber)
        {
            if (cartItems == null || cartItems.Count == 0)
            {
                return null;
            }

            var order = new Order
            {
                OrderId = new Random().Next(1000, 9999),
                Items = new List<Product>(cartItems),
                TotalAmount = cartItems.Sum(i => i.Price)
            };

            foreach (var item in cartItems)
            {
                _inventory.ReserveProduct(item.Id, 1);
            }

            bool paymentResult = await _payment.ProcessPaymentAsync(order.TotalAmount, cardNumber);
            if (!paymentResult)
            {
                throw new PaymentFailedException("Payment was declined by the bank.");
            }

            order.IsPaid = true;
            return order;
        }
    }
}
