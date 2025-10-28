using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using DuckyNet.Shared.RPC;

namespace DuckyNet.Client.RPC
{
    /// <summary>
    /// 客户端上下文实现（用于调用服务器）
    /// </summary>
    public class ClientServerContext : IClientContext
    {
        private readonly RpcClient _client;

        public string ClientId => "local";
        public object NetPeer => _client;
        public ClientSessionState SessionState { get; internal set; } = ClientSessionState.Connected;
        public DateTime LastHeartbeat { get; internal set; } = DateTime.UtcNow;
        public bool IsDisconnected => !_client.IsConnected;
        public int ReconnectCount { get; internal set; } = 0;

        public ClientServerContext(RpcClient client)
        {
            _client = client;
        }

        public void Invoke<TService>(string methodName, params object[] parameters) where TService : class
        {
            _client.InvokeServer<TService>(methodName, parameters);
        }

        public async Task<TResult> InvokeAsync<TService, TResult>(string methodName, params object[] parameters) where TService : class
        {
            return await _client.InvokeServerAsync<TService, TResult>(methodName, parameters);
        }
    }

    /// <summary>
    /// RPC 客户端
    /// </summary>
    public class RpcClient : INetEventListener
    {
        private readonly NetManager _netManager;
        private readonly RpcInvoker _invoker;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<RpcResponse>> _pendingCalls;
        private readonly RpcTimeoutManager _timeoutManager;
        private readonly ConnectionManager _connectionManager;
        private readonly RpcConfig _config;
        private NetPeer? _serverPeer;
        private int _nextMessageId = 1;

        public event Action? Connected;
        public event Action<string>? Disconnected;
        public event Action<RpcConnectionState>? ConnectionStateChanged;

        public bool IsConnected => _connectionManager.State == RpcConnectionState.Connected;
        public RpcConnectionState ConnectionState => _connectionManager.State;

        public RpcClient(RpcConfig? config = null)
        {
            _config = config ?? RpcConfig.Default;
            _netManager = new NetManager(this);
            _invoker = new RpcInvoker();
            _pendingCalls = new ConcurrentDictionary<int, TaskCompletionSource<RpcResponse>>();
            _timeoutManager = new RpcTimeoutManager(_config.DefaultTimeoutMs);
            _connectionManager = new ConnectionManager();

            if (_config.EnableVerboseLogging)
            {
                RpcLog.Info($"[RpcClient] Initialized with timeout: {_config.DefaultTimeoutMs}ms");
            }
        }

        public void RegisterClientService<TService>(object serviceInstance) where TService : class
        {
            _invoker.RegisterService<TService>(serviceInstance);
            RpcLog.Info($"[RpcClient] Registered client service: {typeof(TService).Name}");
        }

        public void Connect(string address, int port)
        {
            _netManager.Start();
            _serverPeer = _netManager.Connect(address, port, string.Empty);
            _connectionManager.SetState(RpcConnectionState.Connecting);
            RpcLog.Info($"[RpcClient] Connecting to {address}:{port}...");
        }

        public void Disconnect()
        {
            _serverPeer?.Disconnect();
            _netManager.Stop();
            _connectionManager.SetState(RpcConnectionState.Disconnected);
            
            // 清理所有超时
            foreach (var kvp in _pendingCalls)
            {
                _timeoutManager.ClearTimeout(kvp.Key);
            }
            
            RpcLog.Info("[RpcClient] Disconnected");
        }

        public void Update()
        {
            _netManager.PollEvents();
        }

        public void InvokeServer<TService>(string methodName, params object[] parameters) where TService : class
        {
            if (_serverPeer == null)
            {
                RpcLog.Error("[RpcClient] Not connected to server");
                return;
            }

            var serviceName = GetServiceName(typeof(TService));
            var message = new RpcMessage
            {
                MessageId = _nextMessageId++,
                ServiceName = serviceName,
                MethodName = methodName,
                Parameters = RpcSerializer.Instance.SerializeParameters(parameters)
            };

            SendMessage(message);

            if (_config.EnableVerboseLogging)
            {
                RpcLog.Info($"[RpcClient] Invoke: {serviceName}.{methodName}");
            }
        }

        public async Task<TResult> InvokeServerAsync<TService, TResult>(string methodName, params object[] parameters) where TService : class
        {
            if (_serverPeer == null)
            {
                throw new InvalidOperationException("Not connected to server");
            }

            var serviceName = GetServiceName(typeof(TService));
            var messageId = _nextMessageId++;
            var message = new RpcMessage
            {
                MessageId = messageId,
                ServiceName = serviceName,
                MethodName = methodName,
                Parameters = RpcSerializer.Instance.SerializeParameters(parameters)
            };

            var tcs = new TaskCompletionSource<RpcResponse>();
            _pendingCalls[messageId] = tcs;

            // 设置超时
            var timeoutToken = _timeoutManager.SetTimeout(messageId, _config.DefaultTimeoutMs);
            timeoutToken.Register(() =>
            {
                if (_pendingCalls.TryRemove(messageId, out var pendingTcs))
                {
                    pendingTcs.TrySetException(new TimeoutException(
                        $"RPC call '{methodName}' timed out after {_config.DefaultTimeoutMs}ms"));
                }
            });

            SendMessage(message);

            if (_config.EnableVerboseLogging)
            {
                RpcLog.Info($"[RpcClient] InvokeAsync: {serviceName}.{methodName}");
            }

            try
            {
                var response = await tcs.Task;
                _timeoutManager.ClearTimeout(messageId);

                if (!response.Success)
                {
                    throw new Exception($"RPC call failed: {response.ErrorMessage}");
                }

                if (response.Result == null)
                {
                    return default!;
                }

                return RpcSerializer.Instance.Deserialize<TResult>(response.Result);
            }
            catch
            {
                _timeoutManager.ClearTimeout(messageId);
                throw;
            }
        }

        private void SendMessage(RpcMessage message)
        {
            if (_serverPeer == null) return;

            var data = RpcSerializer.Instance.Serialize(message);
            _serverPeer.Send(data, DeliveryMethod.ReliableOrdered);
        }

        private void HandleMessage(byte[] data)
        {
            try
            {
                // 尝试反序列化为响应
                RpcResponse? response = null;
                RpcMessage? message = null;

                try
                {
                    response = RpcSerializer.Instance.Deserialize<RpcResponse>(data);
                    if (response != null && _pendingCalls.TryRemove(response.MessageId, out var tcs))
                    {
                        tcs.TrySetResult(response);
                        return;
                    }
                }
                catch
                {
                    // 不是响应消息，尝试作为请求消息处理
                }

                // 尝试反序列化为请求
                message = RpcSerializer.Instance.Deserialize<RpcMessage>(data);
                if (message != null)
                {
                    HandleServerCall(message);
                }
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcClient] Error handling message: {ex.Message}");
            }
        }

        private void HandleServerCall(RpcMessage message)
        {
            try
            {
                // 反序列化参数
                var parameters = message.Parameters != null
                    ? RpcSerializer.Instance.DeserializeParameters(message.Parameters)
                    : Array.Empty<object>();

                // 调用本地服务方法
                var result = _invoker.Invoke(message.ServiceName, message.MethodName, parameters, null);

                // 发送响应
                var response = new RpcResponse
                {
                    MessageId = message.MessageId,
                    Success = true,
                    Result = result != null ? RpcSerializer.Instance.Serialize(result) : null,
                    ErrorMessage = null
                };

                SendResponse(response);
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcClient] Error handling server call: {ex.Message}");

                var errorResponse = new RpcResponse
                {
                    MessageId = message.MessageId,
                    Success = false,
                    Result = null,
                    ErrorMessage = ex.Message
                };

                SendResponse(errorResponse);
            }
        }

        private void SendResponse(RpcResponse response)
        {
            if (_serverPeer == null) return;

            var data = RpcSerializer.Instance.Serialize(response);
            _serverPeer.Send(data, DeliveryMethod.ReliableOrdered);
        }

        private string GetServiceName(Type serviceType)
        {
            var attr = serviceType.GetCustomAttributes(typeof(RpcServiceAttribute), false);
            if (attr.Length > 0 && attr[0] is RpcServiceAttribute rpcAttr)
            {
                return rpcAttr.ServiceName;
            }
            return serviceType.Name;
        }

        #region INetEventListener Implementation

        public void OnPeerConnected(NetPeer peer)
        {
            _serverPeer = peer;
            _connectionManager.SetState(RpcConnectionState.Connected);
            RpcLog.Info($"[RpcClient] Connected to server: {peer.Address}:{peer.Port}");
            Connected?.Invoke();
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _connectionManager.SetState(RpcConnectionState.Disconnected);

            // 清理所有待处理的调用
            foreach (var kvp in _pendingCalls)
            {
                kvp.Value.TrySetException(new Exception("Disconnected from server"));
                _timeoutManager.ClearTimeout(kvp.Key);
            }
            _pendingCalls.Clear();

            RpcLog.Info($"[RpcClient] Disconnected: {disconnectInfo.Reason}");
            Disconnected?.Invoke(disconnectInfo.Reason.ToString());
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            RpcLog.Error($"[RpcClient] Network error: {socketError}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            var data = reader.GetRemainingBytes();
            HandleMessage(data);
        }

        public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
        }

        #endregion
    }
}
