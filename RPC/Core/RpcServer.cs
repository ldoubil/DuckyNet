using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiteNetLib;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Utils;
using RpcMessage = DuckyNet.RPC.Messages.RpcMessage;
using RpcResponse = DuckyNet.RPC.Messages.RpcResponse;
using RpcMessageType = DuckyNet.RPC.Messages.RpcMessageType;
using RpcServiceAttribute = DuckyNet.RPC.Messages.RpcServiceAttribute;
using RpcSerializer = DuckyNet.RPC.Core.RpcSerializer;
using RpcLog = DuckyNet.RPC.Utils.RpcLog;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// 服务器端客户端上下文实现
    /// </summary>
    public class ServerClientContext : Context.IClientContext
    {
        private readonly RpcServer _server;
        private readonly NetPeer _peer;

        public string ClientId { get; }
        public object NetPeer => _peer;

        public Context.ClientSessionState SessionState { get; internal set; } = Context.ClientSessionState.Connected;

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
        private readonly RpcSerializer _serializer;
        private readonly RpcMessageHandler _messageHandler;
        private readonly RpcMessageBuilder _messageBuilder;
        private readonly ConcurrentDictionary<NetPeer, RpcResponseHandler> _responseHandlers;
        private readonly ConcurrentDictionary<NetPeer, ServerClientContext> _clientContexts;
        private readonly ConcurrentDictionary<string, ServerClientContext> _clientContextsById;

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
            _responseHandlers = new ConcurrentDictionary<NetPeer, RpcResponseHandler>();
            _clientContexts = new ConcurrentDictionary<NetPeer, ServerClientContext>();
            _clientContextsById = new ConcurrentDictionary<string, ServerClientContext>();
            _timeoutManager = new RpcTimeoutManager(_config.DefaultTimeoutMs);
            _serializer = RpcSerializer.Instance;
            _messageHandler = new RpcMessageHandler(_serializer);
            _messageBuilder = new RpcMessageBuilder(_serializer);
            
            if (_config.EnableVerboseLogging)
            {
                RpcLog.Info($"[RpcServer] Initialized with max clients: {_config.MaxClients}");
            }
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public void Start(int port)
        {
            _netManager.Start(port);
            RpcLog.Info($"[RpcServer] Server started on port {port}");
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            _netManager.Stop();
            RpcLog.Info("[RpcServer] Server stopped");
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
            if (_config.EnableVerboseLogging)
            {
                RpcLog.Info($"[RpcServer] Registered service: {typeof(TService).Name}");
            }
        }

        /// <summary>
        /// 添加中间件（支持链式调用）
        /// </summary>
        public RpcServer UseMiddleware(IRpcMiddleware middleware)
        {
            _invoker.Use(middleware);
            return this;
        }

        /// <summary>
        /// 注册方法处理函数（支持 next() 调用，可以在多个地方注册）
        /// </summary>
        public RpcServer RegisterMethodHandler<TService>(string methodName, RpcMethodHandler handler) where TService : class
        {
            _invoker.RegisterMethodHandler<TService>(methodName, handler);
            return this;
        }

        /// <summary>
        /// 调用客户端方法（无返回值）
        /// </summary>
        public void InvokeClient<TService>(NetPeer peer, string methodName, params object[] parameters) where TService : class
        {
            var message = _messageBuilder.BuildRequest<TService>(methodName, parameters);
            SendMessage(peer, message);
        }

        /// <summary>
        /// 调用客户端方法（有返回值）
        /// </summary>
        public async Task<TResult> InvokeClientAsync<TService, TResult>(NetPeer peer, string methodName, params object[] parameters) where TService : class
        {
            var messageId = _messageBuilder.GetNextMessageId();
            var message = _messageBuilder.BuildRequest<TService>(messageId, methodName, parameters);

            var responseHandler = _responseHandlers.GetOrAdd(peer, p =>
            {
                var peerCalls = new ConcurrentDictionary<int, TaskCompletionSource<RpcResponse>>();
                return new RpcResponseHandler(peerCalls, _timeoutManager, _config, _serializer);
            });

            var tcs = new TaskCompletionSource<RpcResponse>();
            responseHandler.RegisterPendingCall(messageId, tcs);

            SendMessage(peer, message);

            return await responseHandler.WaitForResponseAsync<TResult>(messageId, tcs);
        }

        /// <summary>
        /// 发送消息给满足条件的客户端（使用过滤器）
        /// </summary>
        public void SendTo<TService>(Func<string, bool> predicate, string methodName, params object[] parameters) where TService : class
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
            if (_clientContextsById.TryGetValue(clientId, out var context) && 
                context.NetPeer is NetPeer peer)
            {
                peer.Disconnect();
                RpcLog.Info($"[RpcServer] Disconnected client {clientId}: {reason}");
            }
        }

        /// <summary>
        /// 获取客户端上下文
        /// </summary>
        public ServerClientContext? GetClientContext(string clientId)
        {
            _clientContextsById.TryGetValue(clientId, out var context);
            return context;
        }
        

        private void SendMessage(NetPeer peer, RpcMessage message)
        {
            var data = _serializer.Serialize(message);
            peer.Send(data, DeliveryMethod.ReliableOrdered);
        }

        private void SendResponse(NetPeer peer, RpcResponse response)
        {
            var data = _serializer.Serialize(response);
            peer.Send(data, DeliveryMethod.ReliableOrdered);
        }

        #region INetEventListener Implementation

        public void OnPeerConnected(NetPeer peer)
        {
            var context = new ServerClientContext(this, peer);
            _clientContexts[peer] = context;
            _clientContextsById[context.ClientId] = context;
            RpcLog.Info($"[RpcServer] Client connected: {peer.Address}:{peer.Port} (ID: {peer.Id})");
            
            // 触发连接事件
            ClientConnected?.Invoke(context.ClientId);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            var clientId = peer.Id.ToString();
            
            if (_clientContexts.TryRemove(peer, out var context))
            {
                _clientContextsById.TryRemove(context.ClientId, out _);
            }
            
            if (_responseHandlers.TryRemove(peer, out var responseHandler))
            {
                responseHandler.CancelAllPendingCalls("Client disconnected");
            }
            
            RpcLog.Info($"[RpcServer] Client disconnected: {peer.Address}:{peer.Port} - {disconnectInfo.Reason}");
            
            // 触发断开连接事件
            ClientDisconnected?.Invoke(clientId);
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            RpcLog.Error($"[RpcServer] Network error: {socketError}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            try
            {
                var data = reader.GetRemainingBytes();
                var result = _messageHandler.HandleMessage(data);

                if (!result.IsValid)
                {
                    if (result.IsError)
                    {
                        RpcLog.Error($"[RpcServer] Error processing message: {result.Exception?.Message}");
                    }
                    return;
                }

                if (result.IsRequest && result.RequestMessage != null)
                {
                    _ = HandleClientCallAsync(peer, result.RequestMessage);
                }
                else if (result.IsResponse && result.ResponseMessage != null)
                {
                    HandleClientResponse(peer, result.ResponseMessage);
                }
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcServer] Error processing message: {ex.Message}");
            }
            finally
            {
                reader.Recycle();
            }
        }

        private async Task HandleClientCallAsync(NetPeer peer, RpcMessage message)
        {
            try
            {
                if (!_clientContexts.TryGetValue(peer, out var clientContext))
                {
                    RpcLog.Error($"[RpcServer] Client context not found for peer {peer.Id}");
                    return;
                }

                // 使用 InvokeAsync 来处理可能的异步方法
                var result = await _invoker.InvokeAsync(message.ServiceName, message.MethodName,
                    _serializer.DeserializeParameters(message.Parameters), clientContext);

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

                // 使用消息构建器创建响应
                var response = _messageBuilder.BuildSuccessResponse(message.MessageId, result);
                SendResponse(peer, response);
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcServer] Error handling client call '{message.MethodName}': {ex.Message}");
                var response = _messageBuilder.BuildErrorResponse(message.MessageId, ex);
                SendResponse(peer, response);
            }
        }

        private void HandleClientResponse(NetPeer peer, RpcResponse response)
        {
            if (_responseHandlers.TryGetValue(peer, out var responseHandler))
            {
                responseHandler.HandleResponse(response);
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

