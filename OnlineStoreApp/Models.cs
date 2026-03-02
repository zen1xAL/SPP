using System;
using System.Collections.Generic;

namespace OnlineStoreApp.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }

    public class Order
    {
        public int OrderId { get; set; }
        public List<Product> Items { get; set; } = new List<Product>();
        public decimal TotalAmount { get; set; }
        public bool IsPaid { get; set; }
    }
}
