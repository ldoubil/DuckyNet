using System;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// RPC 日志接口
    /// </summary>
    public interface IRpcLogger
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    /// <summary>
    /// 控制台日志实现
    /// </summary>
    public class ConsoleRpcLogger : IRpcLogger
    {
        public void Log(string message)
        {
            Console.WriteLine($"[RPC] {message}");
        }

        public void LogWarning(string message)
        {
            Console.WriteLine($"[RPC WARNING] {message}");
        }

        public void LogError(string message)
        {
            Console.WriteLine($"[RPC ERROR] {message}");
        }
    }

    /// <summary>
    /// 静态日志管理器
    /// </summary>
    public static class RpcLog
    {
        private static IRpcLogger _logger = new ConsoleRpcLogger();

        public static void SetLogger(IRpcLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public static void Info(string message) => _logger.Log(message);
        public static void Warning(string message) => _logger.LogWarning(message);
        public static void Error(string message) => _logger.LogError(message);
    }
}

