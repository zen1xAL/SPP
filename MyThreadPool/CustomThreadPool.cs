using System;
using System.Collections.Generic;
using System.Threading;

namespace MyThreadPool
{
    public class CustomThreadPool : IDisposable
    {
        public event Action<string, int, int> OnThreadCreated;  
        public event Action<string, int> OnThreadDestroyed;     
        public event Action<string> OnError;                     

        private readonly int _minThreads;
        private readonly int _maxThreads;
        private readonly int _idleTimeoutMs;
        
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly List<Thread> _workers = new List<Thread>();
        
        private int _activeThreads = 0;
        private int _idleThreads = 0;
        private bool _isDisposed = false;
        private readonly object _lock = new object();

        public int ActiveThreads => _activeThreads;
        public int IdleThreads => _idleThreads;
        public int QueueLength { get { lock (_lock) return _queue.Count; } }

        public CustomThreadPool(int minThreads, int maxThreads, int idleTimeoutMs)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _idleTimeoutMs = idleTimeoutMs;

            lock (_lock)
            {
                for (int i = 0; i < _minThreads; i++) StartNewThread_Unsafe();
            }
        }

        public void Enqueue(Action task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            lock (_lock)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(CustomThreadPool));

                _queue.Enqueue(task);

                if (_idleThreads == 0 && _activeThreads < _maxThreads)
                {
                    StartNewThread_Unsafe();
                    OnThreadCreated?.Invoke("Очередь растет. Создан новый поток.", _activeThreads, _maxThreads);
                }

                Monitor.Pulse(_lock);
            }
        }

        private void StartNewThread_Unsafe()
        {
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"CustomWorker_{Guid.NewGuid().ToString().Substring(0,5)}"
            };
            _workers.Add(thread);
            _activeThreads++;
            thread.Start();
        }

        private void WorkerLoop()
        {
            while (true)
            {
                Action task = null;

                lock (_lock)
                {
                    _idleThreads++;

                    while (_queue.Count == 0 && !_isDisposed)
                    {
                        bool signaled = Monitor.Wait(_lock, _idleTimeoutMs);

                        if (!signaled && _queue.Count == 0 && _activeThreads > _minThreads)
                        {
                            _idleThreads--;
                            _activeThreads--;
                            _workers.Remove(Thread.CurrentThread);
                            
                            OnThreadDestroyed?.Invoke($"Поток {Thread.CurrentThread.Name} простаивал и был завершен.", _activeThreads);
                            return;
                        }
                    }

                    _idleThreads--;
                    if (_isDisposed && _queue.Count == 0) return;
                    task = _queue.Dequeue();
                }

                if (task != null)
                {
                    try { task(); }
                    catch (Exception ex)
                    {
                        OnError?.Invoke($"Поток {Thread.CurrentThread.Name} перехватил сбой: {ex.Message}");
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _isDisposed = true;
                Monitor.PulseAll(_lock);
            }
        }
    }
}