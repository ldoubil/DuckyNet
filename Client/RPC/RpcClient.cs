using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using DuckyNet.Shared.RPC;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.RPC
{
    /// <summary>
    /// 客户端上下文实现（用于调用服务器）
    /// </summary>
    public class ClientServerContext : IClientContext
    {
        private readonly RpcClient _client;

        public string ClientId => GameContext.IsInitialized ? GameContext.Instance.LocalPlayer.Info.SteamId : "local";
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
        private DateTime _connectionStartTime;
        private const int CONNECTION_TIMEOUT_MS = 5000; // 5秒连接超时

        public event Action? Connected;
        public event Action<string>? Disconnected;
        // public event Action<RpcConnectionState>? ConnectionStateChanged;  // 未使用，已注释
        public event Action<string>? ConnectionFailed;

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
                Console.WriteLine($"[RpcClient] Initialized with timeout: {_config.DefaultTimeoutMs}ms");
            }
        }

        public void RegisterClientService<TService>(object serviceInstance) where TService : class
        {
            _invoker.RegisterService<TService>(serviceInstance);
            Console.WriteLine($"[RpcClient] Registered client service: {typeof(TService).Name}");
        }

        public void Connect(string address, int port)
        {
            try
            {
                _netManager.Start();
                _serverPeer = _netManager.Connect(address, port, string.Empty);
                _connectionManager.SetState(RpcConnectionState.Connecting);
                _connectionStartTime = DateTime.UtcNow;
                Console.WriteLine($"[RpcClient] Connecting to {address}:{port}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RpcClient] Connect failed: {ex.Message}");
                _connectionManager.SetState(RpcConnectionState.Disconnected);
                var reason = $"连接失败: {ex.Message}";
                
                // 发布 EventBus 事件
                PublishConnectionFailedEvent(reason);
                
                // 保持向后兼容：同时触发原有事件
                ConnectionFailed?.Invoke(reason);
            }
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
            
            Console.WriteLine("[RpcClient] Disconnected");
        }

        public void Update()
        {
            _netManager.PollEvents();
            
            // 检查连接超时
            if (_connectionManager.State == RpcConnectionState.Connecting)
            {
                var elapsed = (DateTime.UtcNow - _connectionStartTime).TotalMilliseconds;
                if (elapsed > CONNECTION_TIMEOUT_MS)
                {
                    Console.WriteLine($"[RpcClient] Connection timeout after {CONNECTION_TIMEOUT_MS}ms");
                    _connectionManager.SetState(RpcConnectionState.Disconnected);
                    _netManager.Stop();
                    var timeoutReason = "连接超时";
                    
                    // 发布 EventBus 事件
                    PublishConnectionFailedEvent($"连接超时: 无法连接到服务器");
                    PublishDisconnectedEvent(timeoutReason);
                    
                    // 保持向后兼容：同时触发原有事件
                    ConnectionFailed?.Invoke($"连接超时: 无法连接到服务器");
                    Disconnected?.Invoke(timeoutReason);
                }
            }
        }

        public void InvokeServer<TService>(string methodName, params object[] parameters) where TService : class
        {
            if (_serverPeer == null)
            {
                Console.WriteLine("[RpcClient] Not connected to server");
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
                Console.WriteLine($"[RpcClient] Invoke: {serviceName}.{methodName}");
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
                Console.WriteLine($"[RpcClient] InvokeAsync: {serviceName}.{methodName}");
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
                // 使用类型标记检测消息类型（更可靠的方法）
                var messageType = RpcSerializer.Instance.DetectMessageType(data);
                
                if (messageType == RpcMessageType.Response)
                {
                    // 明确是响应消息
                    try
                    {
                        var response = RpcSerializer.Instance.Deserialize<RpcResponse>(data);
                        if (response != null)
                        {
                            if (_pendingCalls.TryRemove(response.MessageId, out var tcs))
                            {
                                tcs.TrySetResult(response);
                                return; // 成功处理响应
                            }
                            else
                            {
                                // 响应消息但没有对应的 pending call（可能已超时或重复）
                                if (_config.EnableVerboseLogging)
                                {
                                    Console.WriteLine($"[RpcClient] Received RpcResponse with MessageId {response.MessageId}, but no pending call found (may be timeout or duplicate)");
                                }
                                return; // 忽略无匹配的响应
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RpcClient] Failed to deserialize RpcResponse: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"[RpcClient] Inner exception: {ex.InnerException.Message}");
                        }
                    }
                }
                else if (messageType == RpcMessageType.Request)
                {
                    // 明确是请求消息（服务器调用客户端）
                    try
                    {
                        var message = RpcSerializer.Instance.Deserialize<RpcMessage>(data);
                        if (message != null)
                        {
                            HandleServerCall(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RpcClient] Failed to deserialize RpcMessage: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            Console.WriteLine($"[RpcClient] Inner exception: {ex.InnerException.Message}");
                        }
                    }
                }
                else
                {
                    // 没有类型标记或类型未知，尝试兼容旧格式（向后兼容）
                    // 先尝试作为响应（通常响应更常见）
                    try
                    {
                        var response = RpcSerializer.Instance.Deserialize<RpcResponse>(data);
                        if (response != null && _pendingCalls.TryRemove(response.MessageId, out var tcs))
                        {
                            tcs.TrySetResult(response);
                            return;
                        }
                    }
                    catch
                    {
                        // 不是响应，继续尝试作为请求
                    }

                    // 尝试作为请求消息
                    try
                    {
                        var message = RpcSerializer.Instance.Deserialize<RpcMessage>(data);
                        if (message != null)
                        {
                            HandleServerCall(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RpcClient] Failed to deserialize message (unknown type): {ex.Message}");
                        Console.WriteLine($"[RpcClient] Data length: {data?.Length ?? 0} bytes");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RpcClient] Error handling message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    RpcLog.Error($"[RpcClient] Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private void HandleServerCall(RpcMessage message)
        {
            try
            {
                // 反序列化参数
                object?[]? parameters;
                try
                {
                    parameters = message.Parameters != null
                        ? RpcSerializer.Instance.DeserializeParameters(message.Parameters)
                        : Array.Empty<object>();
                    
                    // 调试日志：打印反序列化后的参数类型（仅在详细模式下）
                    if (parameters != null && _config.EnableVerboseLogging)
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            Console.WriteLine($"[RpcClient] 反序列化参数[{i}]: {parameters[i]?.GetType().FullName ?? "null"} = {parameters[i]}");
                        }
                    }
                }
                catch (Exception deserEx)
                {
                    Console.WriteLine($"[RpcClient] 反序列化参数失败: {deserEx.Message}");
                    Console.WriteLine($"[RpcClient] 方法: {message.ServiceName}.{message.MethodName}");
                    Console.WriteLine($"[RpcClient] 参数数据长度: {message.Parameters?.Length ?? 0} bytes");
                    if (deserEx.InnerException != null)
                    {
                        Console.WriteLine($"[RpcClient] 内部异常: {deserEx.InnerException.Message}");
                        if (deserEx.InnerException.StackTrace != null)
                        {
                            Console.WriteLine($"[RpcClient] 内部堆栈: {deserEx.InnerException.StackTrace}");
                        }
                    }
                    if (deserEx.StackTrace != null)
                    {
                        Console.WriteLine($"[RpcClient] 堆栈跟踪: {deserEx.StackTrace}");
                    }
                    throw;
                }

                // 调用本地服务方法
                object? result;
                try
                {
                    result = _invoker.Invoke(message.ServiceName, message.MethodName, parameters, null);
                }
                catch (Exception invokeEx)
                {
                    Console.WriteLine($"[RpcClient] 调用方法失败: {message.ServiceName}.{message.MethodName}");
                    Console.WriteLine($"[RpcClient] 参数数量: {parameters?.Length ?? 0}");
                    if (parameters != null)
                    {
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            Console.WriteLine($"[RpcClient]   参数[{i}]: {parameters[i]?.GetType().FullName ?? "null"} = {parameters[i]}");
                        }
                    }
                    Console.WriteLine($"[RpcClient] 错误: {invokeEx.Message}");
                    Console.WriteLine($"[RpcClient] 堆栈: {invokeEx.StackTrace}");
                    throw;
                }

                // 检查是否是 VoidTaskResult（不应该被序列化）
                var resultType = result?.GetType();
                if (resultType != null && resultType.Name == "VoidTaskResult")
                {
                    result = null;
                }

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
                Console.WriteLine($"[RpcClient] Error handling server call '{message.ServiceName}.{message.MethodName}': {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[RpcClient] 内部异常: {ex.InnerException.Message}");
                }
                Console.WriteLine($"[RpcClient] Stack trace: {ex.StackTrace}");

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
            
            // 发布 EventBus 事件（通过 NetworkLifecycleManager 来发布，这里只触发 RpcClient 事件）
            // NetworkLifecycleManager 会监听 Connected 事件并发布 NetworkConnectedEvent
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
            var reason = disconnectInfo.Reason.ToString();
            
            // 发布 EventBus 事件（通过 NetworkLifecycleManager 来发布）
            // NetworkLifecycleManager 会监听 Disconnected 事件并发布 NetworkDisconnectedEvent
            Disconnected?.Invoke(reason);
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            RpcLog.Error($"[RpcClient] Network error: {socketError}");
            
            // 如果正在连接中，将网络错误视为连接失败
            if (_connectionManager.State == RpcConnectionState.Connecting)
            {
                _connectionManager.SetState(RpcConnectionState.Disconnected);
                _netManager.Stop();
                string errorMessage = GetSocketErrorMessage(socketError);
                
                // 发布 EventBus 事件
                PublishConnectionFailedEvent(errorMessage);
                PublishDisconnectedEvent(errorMessage);
                
                // 保持向后兼容：同时触发原有事件
                ConnectionFailed?.Invoke(errorMessage);
                Disconnected?.Invoke(errorMessage);
            }
        }
        
        /// <summary>
        /// 发布连接失败事件到 EventBus
        /// </summary>
        private void PublishConnectionFailedEvent(string reason)
        {
            try
            {
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.EventBus.Publish(new NetworkConnectionFailedEvent(reason));
                }
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcClient] 发布连接失败事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发布断开连接事件到 EventBus
        /// </summary>
        private void PublishDisconnectedEvent(string reason)
        {
            try
            {
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.EventBus.Publish(new NetworkDisconnectedEvent(reason));
                }
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcClient] 发布断开连接事件失败: {ex.Message}");
            }
        }

        private string GetSocketErrorMessage(System.Net.Sockets.SocketError error)
        {
            return error switch
            {
                System.Net.Sockets.SocketError.ConnectionRefused => "连接被拒绝: 服务器未运行或端口错误",
                System.Net.Sockets.SocketError.HostNotFound => "主机未找到: 服务器地址无效",
                System.Net.Sockets.SocketError.HostUnreachable => "主机不可达: 无法访问服务器",
                System.Net.Sockets.SocketError.NetworkUnreachable => "网络不可达: 检查网络连接",
                System.Net.Sockets.SocketError.TimedOut => "连接超时: 服务器无响应",
                _ => $"网络错误: {error}"
            };
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
