using System;
using System.Collections.Generic;

namespace CalculatorApp
{
    public class DatabaseService
    {
        public bool IsConnected { get; private set; }
        public List<string> History { get; private set; }

        public void Connect()
        {
            IsConnected = true;
            History = new List<string>();
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public void SaveOperation(string operation)
        {
            if (!IsConnected) throw new InvalidOperationException("Not connected");
            History.Add(operation);
        }
    }
}
