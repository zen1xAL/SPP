using System;
using System.Threading.Tasks;

namespace CalculatorApp
{
    public class Calculator
    {
        public int Add(int a, int b) => a + b;
        
        public int Subtract(int a, int b) => a - b;

        public int Multiply(int a, int b) => a * b;

        public double Divide(int a, int b)
        {
            if (b == 0) throw new DivideByZeroException("Cannot divide by zero.");
            return (double)a / b;
        }

        public async Task<int> CalculateAsync(int a, int b, int delayMs = 100)
        {
            await Task.Delay(delayMs);
            return a + b;
        }
        
        public string GetGreeting(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return $"Hello, {name}!";
        }
    }
}
