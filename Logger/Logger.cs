using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Logger
{
    public class LoggerService
    {
        private static readonly Lazy<LoggerService> _instance = new Lazy<LoggerService>(() => new LoggerService());
        public static LoggerService Instance => _instance.Value;

        // Skrót do LoggerService.Instance.Log
        public static void Log(string message) => Instance.EnqueueLog(message);

        private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private readonly List<string> _logHistory = new List<string>();
        private readonly string _logFilePath = "log.txt";
        private readonly object _fileLock = new object();
        private readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(200);
        private CancellationTokenSource? _cts;
        private Task? _backgroundTask;

        public event Action<string>? LogMessageAppended;
        public event Action<IReadOnlyList<string>>? LogHistoryLoaded;

        public bool EnableLogging { get; set; } = true;

        private LoggerService()
        {
            // Start background log flusher
            StartBackgroundWorker();
        }

        public void StartBackgroundWorker()
        {
            if (_backgroundTask != null && !_backgroundTask.IsCompleted)
                return;
            _cts = new CancellationTokenSource();
            _backgroundTask = Task.Run(async () => await ProcessQueueAsync(_cts.Token));
        }

        public void StopBackgroundWorker()
        {
            _cts?.Cancel();
            _backgroundTask?.Wait();
        }

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (_logQueue.TryDequeue(out var msg))
                {
                    if (EnableLogging)
                    {
                        lock (_fileLock)
                        {
                            File.AppendAllText(_logFilePath, msg + Environment.NewLine, Encoding.UTF8);
                        }
                        _logHistory.Add(msg);
                    }
                    LogMessageAppended?.Invoke(msg);
                }
                await Task.Delay(_flushInterval, token);
            }
        }

        public void LoadLogHistory()
        {
            lock (_fileLock)
            {
                if (File.Exists(_logFilePath))
                {
                    var lines = File.ReadAllLines(_logFilePath, Encoding.UTF8);
                    _logHistory.Clear();
                    _logHistory.AddRange(lines);
                    LogHistoryLoaded?.Invoke(_logHistory);
                }
            }
        }

        public IReadOnlyList<string> GetLogHistory() => _logHistory.AsReadOnly();

        private void EnqueueLog(string message)
        {
            if (EnableLogging)
            {
                var timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                _logQueue.Enqueue(timestamped);
            }
        }
    }
}