using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Utils;
using RpcResponse = DuckyNet.RPC.Messages.RpcResponse;
using RpcSerializer = DuckyNet.RPC.Core.RpcSerializer;
using RpcLog = DuckyNet.RPC.Utils.RpcLog;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 响应处理器 - 统一处理异步调用的响应
    /// </summary>
    public class RpcResponseHandler
    {
        private readonly ConcurrentDictionary<int, TaskCompletionSource<RpcResponse>> _pendingCalls;
        private readonly RpcTimeoutManager _timeoutManager;
        private readonly RpcConfig _config;
        private readonly RpcSerializer _serializer;

        public RpcResponseHandler(
            ConcurrentDictionary<int, TaskCompletionSource<RpcResponse>> pendingCalls,
            RpcTimeoutManager timeoutManager,
            RpcConfig config,
            RpcSerializer serializer)
        {
            _pendingCalls = pendingCalls ?? throw new ArgumentNullException(nameof(pendingCalls));
            _timeoutManager = timeoutManager ?? throw new ArgumentNullException(nameof(timeoutManager));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// 注册待处理的异步调用
        /// </summary>
        public void RegisterPendingCall(int messageId, TaskCompletionSource<RpcResponse> tcs)
        {
            _pendingCalls[messageId] = tcs;

            // 设置超时
            var timeoutToken = _timeoutManager.SetTimeout(messageId, _config.DefaultTimeoutMs);
            timeoutToken.Register(() =>
            {
                if (_pendingCalls.TryRemove(messageId, out var pendingTcs))
                {
                    pendingTcs.TrySetException(new TimeoutException(
                        $"RPC call timed out after {_config.DefaultTimeoutMs}ms"));
                }
            });
        }

        /// <summary>
        /// 处理响应消息
        /// </summary>
        public void HandleResponse(RpcResponse response)
        {
            if (_pendingCalls.TryRemove(response.MessageId, out var tcs))
            {
                _timeoutManager.ClearTimeout(response.MessageId);
                tcs.SetResult(response);
            }
        }

        /// <summary>
        /// 等待响应并反序列化结果
        /// </summary>
        public async Task<TResult> WaitForResponseAsync<TResult>(int messageId, TaskCompletionSource<RpcResponse> tcs)
        {
            try
            {
                var response = await tcs.Task.ConfigureAwait(false);
                _timeoutManager.ClearTimeout(messageId);

                if (!response.Success)
                {
                    throw new RpcException($"RPC call failed: {response.ErrorMessage}", response.ErrorMessage);
                }

                if (response.Result == null)
                {
                    return default!;
                }

                return _serializer.Deserialize<TResult>(response.Result);
            }
            catch
            {
                _timeoutManager.ClearTimeout(messageId);
                throw;
            }
        }

        /// <summary>
        /// 取消所有待处理的调用（例如断开连接时）
        /// </summary>
        public void CancelAllPendingCalls(string reason)
        {
            foreach (var kvp in _pendingCalls)
            {
                _timeoutManager.ClearTimeout(kvp.Key);
                kvp.Value.TrySetException(new Exception(reason));
            }
            _pendingCalls.Clear();
        }
    }

    /// <summary>
    /// RPC 异常
    /// </summary>
    public class RpcException : Exception
    {
        public string? RpcErrorMessage { get; }

        public RpcException(string message, string? rpcErrorMessage = null) 
            : base(message)
        {
            RpcErrorMessage = rpcErrorMessage;
        }
    }
}

