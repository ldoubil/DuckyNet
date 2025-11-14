using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using LiteNetLib;
using DuckyNet.RPC.Messages;
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
    /// RPC 客户端
    /// </summary>
    public class RpcClient : INetEventListener
    {
        private readonly NetManager _netManager;
        private readonly RpcInvoker _invoker;
        private readonly RpcTimeoutManager _timeoutManager;
        private readonly RpcConfig _config;
        private readonly RpcSerializer _serializer;
        private readonly RpcMessageHandler _messageHandler;
        private readonly RpcMessageBuilder _messageBuilder;
        private readonly RpcResponseHandler _responseHandler;
        private NetPeer? _serverPeer;
        private DateTime _connectionStartTime;
        private RpcConnectionState _connectionState = RpcConnectionState.Disconnected;
        private const int CONNECTION_TIMEOUT_MS = 5000;

        public event Action? Connected;
        public event Action<string>? Disconnected;
        public event Action<string>? ConnectionFailed;

        public bool IsConnected => _connectionState == RpcConnectionState.Connected;
        public RpcConnectionState ConnectionState => _connectionState;

        public RpcClient(RpcConfig? config = null)
        {
            _config = config ?? RpcConfig.Default;
            _netManager = new NetManager(this);
            _invoker = new RpcInvoker();
            _timeoutManager = new RpcTimeoutManager(_config.DefaultTimeoutMs);
            _serializer = RpcSerializer.Instance;
            _messageHandler = new RpcMessageHandler(_serializer);
            _messageBuilder = new RpcMessageBuilder(_serializer);
            
            var pendingCalls = new ConcurrentDictionary<int, TaskCompletionSource<RpcResponse>>();
            _responseHandler = new RpcResponseHandler(pendingCalls, _timeoutManager, _config, _serializer);
        }

        public void RegisterClientService<TService>(object serviceInstance) where TService : class
        {
            _invoker.RegisterService<TService>(serviceInstance);
        }

        /// <summary>
        /// 添加中间件（支持链式调用）
        /// </summary>
        public RpcClient UseMiddleware(IRpcMiddleware middleware)
        {
            _invoker.Use(middleware);
            return this;
        }

        /// <summary>
        /// 注册方法处理函数（支持 next() 调用，可以在多个地方注册）
        /// </summary>
        public RpcClient RegisterMethodHandler<TService>(string methodName, RpcMethodHandler handler) where TService : class
        {
            _invoker.RegisterMethodHandler<TService>(methodName, handler);
            return this;
        }

        public void Connect(string address, int port)
        {
            try
            {
                _netManager.Start();
                _serverPeer = _netManager.Connect(address, port, string.Empty);
                _connectionState = RpcConnectionState.Connecting;
                _connectionStartTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _connectionState = RpcConnectionState.Disconnected;
                ConnectionFailed?.Invoke($"连接失败: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            _serverPeer?.Disconnect();
            _netManager.Stop();
            _connectionState = RpcConnectionState.Disconnected;
            
            _responseHandler.CancelAllPendingCalls("Disconnected from server");
        }

        public void Update()
        {
            _netManager.PollEvents();
            
            if (_connectionState == RpcConnectionState.Connecting)
            {
                var elapsed = (DateTime.UtcNow - _connectionStartTime).TotalMilliseconds;
                if (elapsed > CONNECTION_TIMEOUT_MS)
                {
                    _connectionState = RpcConnectionState.Disconnected;
                    _netManager.Stop();
                    ConnectionFailed?.Invoke("连接超时: 无法连接到服务器");
                    Disconnected?.Invoke("连接超时");
                }
            }
        }

        public void InvokeServer<TService>(string methodName, params object[] parameters) where TService : class
        {
            if (_serverPeer == null) return;

            var message = _messageBuilder.BuildRequest<TService>(methodName, parameters);
            SendMessage(message);
        }

        public async Task<TResult> InvokeServerAsync<TService, TResult>(string methodName, params object[] parameters) where TService : class
        {
            if (_serverPeer == null)
            {
                throw new InvalidOperationException("Not connected to server");
            }

            var messageId = _messageBuilder.GetNextMessageId();
            var message = _messageBuilder.BuildRequest<TService>(messageId, methodName, parameters);

            var tcs = new TaskCompletionSource<RpcResponse>();
            _responseHandler.RegisterPendingCall(messageId, tcs);

            SendMessage(message);

            return await _responseHandler.WaitForResponseAsync<TResult>(messageId, tcs);
        }

        private void SendMessage(RpcMessage message)
        {
            if (_serverPeer == null) return;
            var data = _serializer.Serialize(message);
            _serverPeer.Send(data, DeliveryMethod.ReliableOrdered);
        }

        private void HandleMessage(byte[] data)
        {
            try
            {
                var result = _messageHandler.HandleMessage(data);

                if (!result.IsValid)
                {
                    if (result.IsError)
                    {
                        RpcLog.Error($"[RpcClient] Error processing message: {result.Exception?.Message}");
                    }
                    return;
                }

                if (result.IsResponse && result.ResponseMessage != null)
                {
                    _responseHandler.HandleResponse(result.ResponseMessage);
                }
                else if (result.IsRequest && result.RequestMessage != null)
                {
                    HandleServerCall(result.RequestMessage);
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
                var parameters = message.Parameters != null
                    ? _serializer.DeserializeParameters(message.Parameters)
                    : Array.Empty<object>();

                var result = _invoker.Invoke(message.ServiceName, message.MethodName, parameters, null);

                var resultType = result?.GetType();
                if (resultType != null && resultType.Name == "VoidTaskResult")
                {
                    result = null;
                }

                var response = _messageBuilder.BuildSuccessResponse(message.MessageId, result);
                SendResponse(response);
            }
            catch (Exception ex)
            {
                var errorResponse = _messageBuilder.BuildErrorResponse(message.MessageId, ex);
                SendResponse(errorResponse);
            }
        }

        private void SendResponse(RpcResponse response)
        {
            if (_serverPeer == null) return;
            var data = _serializer.Serialize(response);
            _serverPeer.Send(data, DeliveryMethod.ReliableOrdered);
        }


        #region INetEventListener Implementation

        public void OnPeerConnected(NetPeer peer)
        {
            _serverPeer = peer;
            _connectionState = RpcConnectionState.Connected;
            Connected?.Invoke();
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _connectionState = RpcConnectionState.Disconnected;

            _responseHandler.CancelAllPendingCalls("Disconnected from server");

            Disconnected?.Invoke(disconnectInfo.Reason.ToString());
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            if (_connectionState == RpcConnectionState.Connecting)
            {
                _connectionState = RpcConnectionState.Disconnected;
                _netManager.Stop();
                var errorMessage = GetSocketErrorMessage(socketError);
                ConnectionFailed?.Invoke(errorMessage);
                Disconnected?.Invoke(errorMessage);
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

