using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using DuckyNet.Shared.RPC;

namespace DuckyNet.Server.RPC
{
    /// <summary>
    /// 服务器端客户端上下文实现
    /// </summary>
    public class ServerClientContext : IClientContext
    {
        private readonly RpcServer _server;
        private readonly NetPeer _peer;

        public string ClientId { get; }
        public object NetPeer => _peer;

        public ClientSessionState SessionState { get; internal set; } = ClientSessionState.Connected;

        public DateTime LastHeartbeat { get; internal set; } = DateTime.UtcNow;

        public bool IsDisconnected => _peer.ConnectionState != LiteNetLib.ConnectionState.Connected;

        public int ReconnectCount { get; internal set; } = 0;

        public ServerClientContext(RpcServer server, NetPeer peer)
        {
            _server = server;
            _peer = peer;
            ClientId = peer.Id.ToString();
        }

        public void Invoke<TService>(string methodName, params object[] parameters) where TService : class
        {
            _server.InvokeClient<TService>(_peer, methodName, parameters);
        }

        public async Task<TResult> InvokeAsync<TService, TResult>(string methodName, params object[] parameters) where TService : class
        {
            return await _server.InvokeClientAsync<TService, TResult>(_peer, methodName, parameters);
        }
    }

    /// <summary>
    /// RPC 服务器
    /// 负责处理客户端连接、调用和响应
    /// </summary>
    public class RpcServer : INetEventListener
    {
        private readonly NetManager _netManager;
        private readonly RpcInvoker _invoker;
        private readonly RpcTimeoutManager _timeoutManager;
        private readonly RpcConfig _config;
        private readonly Dictionary<NetPeer, Dictionary<int, TaskCompletionSource<RpcResponse>>> _pendingCalls;
        private readonly Dictionary<NetPeer, ServerClientContext> _clientContexts;
        private int _nextMessageId = 1;

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        public event Action<string>? ClientConnected;

        /// <summary>
        /// 客户端断开连接事件
        /// </summary>
        public event Action<string>? ClientDisconnected;

        public RpcServer(RpcConfig? config = null)
        {
            _config = config ?? RpcConfig.Default;
            _netManager = new NetManager(this);
            _invoker = new RpcInvoker();
            _pendingCalls = new Dictionary<NetPeer, Dictionary<int, TaskCompletionSource<RpcResponse>>>();
            _clientContexts = new Dictionary<NetPeer, ServerClientContext>();
            _timeoutManager = new RpcTimeoutManager(_config.DefaultTimeoutMs);
            
            if (_config.EnableVerboseLogging)
            {
                Console.WriteLine($"[RpcServer] Initialized with max clients: {_config.MaxClients}");
            }
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public void Start(int port)
        {
            _netManager.Start(port);
            Console.WriteLine($"[RpcServer] Server started on port {port}");
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _netManager.Stop();
            Console.WriteLine("[RpcServer] Server stopped");
        }

        /// <summary>
        /// 更新网络（需要在主循环中调用）
        /// </summary>
        public void Update()
        {
            _netManager.PollEvents();
        }

        /// <summary>
        /// 注册服务器服务（用于接收客户端的RPC调用）
        /// </summary>
        public void RegisterServerService<TService>(object serviceInstance) where TService : class
        {
            _invoker.RegisterService<TService>(serviceInstance);
            Console.WriteLine($"[RpcServer] Registered service: {typeof(TService).Name}");
        }

        /// <summary>
        /// 调用客户端方法（无返回值）
        /// </summary>
        public void InvokeClient<TService>(NetPeer peer, string methodName, params object[] parameters) where TService : class
        {
            var serviceName = GetServiceName(typeof(TService));
            var message = new RpcMessage
            {
                MessageId = _nextMessageId++,
                ServiceName = serviceName,
                MethodName = methodName,
                Parameters = RpcSerializer.Instance.SerializeParameters(parameters)
            };

            SendMessage(peer, message);
        }

        /// <summary>
        /// 调用客户端方法（有返回值）
        /// </summary>
        public async Task<TResult> InvokeClientAsync<TService, TResult>(NetPeer peer, string methodName, params object[] parameters) where TService : class
        {
            var serviceName = GetServiceName(typeof(TService));
            var messageId = _nextMessageId++;
            var message = new RpcMessage
            {
                MessageId = messageId,
                ServiceName = serviceName,
                MethodName = methodName,
                Parameters = RpcSerializer.Instance.SerializeParameters(parameters)
            };

            if (!_pendingCalls.ContainsKey(peer))
            {
                _pendingCalls[peer] = new Dictionary<int, TaskCompletionSource<RpcResponse>>();
            }

            var tcs = new TaskCompletionSource<RpcResponse>();
            _pendingCalls[peer][messageId] = tcs;

            // 设置超时
            var timeoutToken = _timeoutManager.SetTimeout(messageId, _config.DefaultTimeoutMs);
            timeoutToken.Register(() =>
            {
                if (_pendingCalls.TryGetValue(peer, out var peerCalls) &&
                    peerCalls.TryGetValue(messageId, out var pendingTcs))
                {
                    peerCalls.Remove(messageId);
                    pendingTcs.TrySetException(new TimeoutException(
                        $"RPC call to client '{methodName}' timed out after {_config.DefaultTimeoutMs}ms"));
                }
            });

            SendMessage(peer, message);

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

        /// <summary>
        /// 广播消息给所有客户端
        /// </summary>
        public void BroadcastToAll<TService>(string methodName, params object[] parameters) where TService : class
        {
            foreach (var peer in _netManager.ConnectedPeerList)
            {
                InvokeClient<TService>(peer, methodName, parameters);
            }
        }

        /// <summary>
        /// 广播消息给指定的客户端列表
        /// </summary>
        public void BroadcastToClients<TService>(IEnumerable<string> clientIds, string methodName, params object[] parameters) where TService : class
        {
            foreach (var clientId in clientIds)
            {
                var context = GetClientContext(clientId);
                if (context != null && context.NetPeer is NetPeer peer)
                {
                    InvokeClient<TService>(peer, methodName, parameters);
                }
            }
        }

        /// <summary>
        /// 广播消息给满足条件的客户端（使用过滤器）
        /// </summary>
        public void BroadcastWhere<TService>(Func<string, bool> predicate, string methodName, params object[] parameters) where TService : class
        {
            foreach (var kvp in _clientContexts)
            {
                if (predicate(kvp.Value.ClientId))
                {
                    InvokeClient<TService>(kvp.Key, methodName, parameters);
                }
            }
        }

        /// <summary>
        /// 断开指定客户端连接
        /// </summary>
        public void DisconnectClient(string clientId, string reason)
        {
            foreach (var kvp in _clientContexts)
            {
                if (kvp.Value.ClientId == clientId)
                {
                    kvp.Key.Disconnect();
                    RpcLog.Info($"[RpcServer] Disconnected client {clientId}: {reason}");
                    return;
                }
            }
        }

        /// <summary>
        /// 获取客户端上下文
        /// </summary>
        public ServerClientContext? GetClientContext(string clientId)
        {
            foreach (var kvp in _clientContexts)
            {
                if (kvp.Value.ClientId == clientId)
                {
                    return kvp.Value;
                }
            }
            return null;
        }
        

        private void SendMessage(NetPeer peer, RpcMessage message)
        {
            var data = RpcSerializer.Instance.Serialize(message);
            peer.Send(data, DeliveryMethod.ReliableOrdered);
        }

        private void SendResponse(NetPeer peer, RpcResponse response)
        {
            var data = RpcSerializer.Instance.Serialize(response);
            peer.Send(data, DeliveryMethod.ReliableOrdered);
        }

        private string GetServiceName(Type serviceType)
        {
            var attr = serviceType.GetCustomAttributes(typeof(RpcServiceAttribute), false);
            if (attr.Length > 0)
            {
                return ((RpcServiceAttribute)attr[0]).ServiceName;
            }
            return serviceType.Name;
        }

        #region INetEventListener Implementation

        public void OnPeerConnected(NetPeer peer)
        {
            var context = new ServerClientContext(this, peer);
            _clientContexts[peer] = context;
            RpcLog.Info($"[RpcServer] Client connected: {peer.Address}:{peer.Port} (ID: {peer.Id})");
            
            // 触发连接事件
            ClientConnected?.Invoke(context.ClientId);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            var clientId = peer.Id.ToString();
            
            _clientContexts.Remove(peer);
            _pendingCalls.Remove(peer);
            
            Console.WriteLine($"[RpcServer] Client disconnected: {peer.Address}:{peer.Port} - {disconnectInfo.Reason}");
            
            // 触发断开连接事件
            ClientDisconnected?.Invoke(clientId);
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            Console.WriteLine($"[RpcServer] Network error: {socketError}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            try
            {
                var data = reader.GetRemainingBytes();

                // 使用类型标记检测消息类型（更可靠的方法）
                var messageType = RpcSerializer.Instance.DetectMessageType(data);
                
                if (messageType == RpcMessageType.Request)
                {
                    // 明确是请求消息（客户端调用服务器）
                    try
                    {
                        var message = RpcSerializer.Instance.Deserialize<RpcMessage>(data);
                        HandleClientCall(peer, message);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RpcServer] Failed to deserialize RpcMessage: {ex.Message}");
                    }
                }
                else if (messageType == RpcMessageType.Response)
                {
                    // 明确是响应消息（客户端响应服务器调用）
                    try
                    {
                        var response = RpcSerializer.Instance.Deserialize<RpcResponse>(data);
                        HandleClientResponse(peer, response);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RpcServer] Failed to deserialize RpcResponse: {ex.Message}");
                    }
                }
                else
                {
                    // 没有类型标记或类型未知，尝试兼容旧格式（向后兼容）
                    // 先尝试作为请求（服务器接收的通常是请求）
                    try
                    {
                        var message = RpcSerializer.Instance.Deserialize<RpcMessage>(data);
                        HandleClientCall(peer, message);
                        return;
                    }
                    catch
                    {
                        // 不是请求，继续尝试作为响应
                    }

                    // 尝试作为响应
                    try
                    {
                        var response = RpcSerializer.Instance.Deserialize<RpcResponse>(data);
                        HandleClientResponse(peer, response);
                        return;
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"[RpcServer] Failed to parse message: {ex2.Message}");
                    }

                    Console.WriteLine("[RpcServer] Received unknown message type");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RpcServer] Error processing message: {ex.Message}");
            }
            finally
            {
                reader.Recycle();
            }
        }

        private async void HandleClientCall(NetPeer peer, RpcMessage message)
        {
            try
            {
                var clientContext = _clientContexts[peer];
                // 使用 InvokeAsync 来处理可能的异步方法
                var result = await _invoker.InvokeAsync(message.ServiceName, message.MethodName,
                    RpcSerializer.Instance.DeserializeParameters(message.Parameters), clientContext);

                // 确保 result 不是 Task 类型（安全检查）
                if (result is Task)
                {
                    throw new InvalidOperationException($"Method '{message.MethodName}' returned a Task instead of the actual result. This should not happen.");
                }

                // 检查是否是 VoidTaskResult（Task 的内部类型，不应该被序列化）
                var resultType = result?.GetType();
                if (resultType != null && resultType.Name == "VoidTaskResult")
                {
                    // VoidTaskResult 不应该被序列化，对于 void/Task 方法，返回 null
                    result = null;
                }

                // 不记录返回类型日志（void/Task 方法返回 null 是正常的，不需要记录）
                // 如果需要调试，可以临时取消注释下面这行：
                // if (_config.EnableVerboseLogging && result != null)
                // {
                //     RpcLog.Info($"[RpcServer] Method '{message.MethodName}' returned type: {result.GetType().FullName}");
                // }

                // 对于 void 或 Task 方法，result 应该是 null，不需要序列化
                var response = new RpcResponse
                {
                    MessageId = message.MessageId,
                    Success = true,
                    Result = result != null ? RpcSerializer.Instance.Serialize(result) : null
                };

                SendResponse(peer, response);
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcServer] Error handling client call '{message.MethodName}': {ex.Message}");
                var response = new RpcResponse
                {
                    MessageId = message.MessageId,
                    Success = false,
                    ErrorMessage = ex.Message
                };

                SendResponse(peer, response);
            }
        }

        private void HandleClientResponse(NetPeer peer, RpcResponse response)
        {
            if (_pendingCalls.TryGetValue(peer, out var peerCalls))
            {
                if (peerCalls.TryGetValue(response.MessageId, out var tcs))
                {
                    peerCalls.Remove(response.MessageId);
                    tcs.SetResult(response);
                }
            }
        }

        public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if (_netManager.ConnectedPeersCount < _config.MaxClients)
            {
                request.Accept();
            }
            else
            {
                RpcLog.Warning($"[RpcServer] Connection rejected: max clients ({_config.MaxClients}) reached");
                request.Reject();
            }
        }

        #endregion
    }
}
