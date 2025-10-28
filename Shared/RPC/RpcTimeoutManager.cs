using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// RPC 调用超时管理器
    /// </summary>
    public class RpcTimeoutManager
    {
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _timeouts = new ConcurrentDictionary<int, CancellationTokenSource>();
        private readonly int _defaultTimeoutMs;

        public RpcTimeoutManager(int defaultTimeoutMs = 30000)
        {
            _defaultTimeoutMs = defaultTimeoutMs;
        }

        /// <summary>
        /// 为消息ID设置超时
        /// </summary>
        public CancellationToken SetTimeout(int messageId, int timeoutMs = -1)
        {
            if (timeoutMs <= 0)
                timeoutMs = _defaultTimeoutMs;

            var cts = new CancellationTokenSource(timeoutMs);
            _timeouts[messageId] = cts;

            // 超时时自动清理
            cts.Token.Register(() => ClearTimeout(messageId));

            return cts.Token;
        }

        /// <summary>
        /// 清除超时设置
        /// </summary>
        public void ClearTimeout(int messageId)
        {
            if (_timeouts.TryRemove(messageId, out var cts))
            {
                cts.Dispose();
            }
        }

        /// <summary>
        /// 检查消息是否已超时
        /// </summary>
        public bool IsTimedOut(int messageId)
        {
            if (_timeouts.TryGetValue(messageId, out var cts))
            {
                return cts.Token.IsCancellationRequested;
            }
            return false;
        }

        /// <summary>
        /// 清理所有超时设置
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _timeouts)
            {
                kvp.Value.Dispose();
            }
            _timeouts.Clear();
        }
    }
}