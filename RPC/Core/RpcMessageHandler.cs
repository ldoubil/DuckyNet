using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Utils;
using RpcMessage = DuckyNet.RPC.Messages.RpcMessage;
using RpcResponse = DuckyNet.RPC.Messages.RpcResponse;
using RpcMessageType = DuckyNet.RPC.Messages.RpcMessageType;
using RpcSerializer = DuckyNet.RPC.Core.RpcSerializer;
using RpcLog = DuckyNet.RPC.Utils.RpcLog;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 消息处理器 - 统一处理消息路由和反序列化
    /// </summary>
    public class RpcMessageHandler
    {
        private readonly RpcSerializer _serializer;

        public RpcMessageHandler(RpcSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// 处理接收到的消息数据
        /// </summary>
        public MessageHandleResult HandleMessage(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return MessageHandleResult.Invalid;
            }

            try
            {
                var messageType = _serializer.DetectMessageType(data);
                
                if (messageType == null)
                {
                    RpcLog.Warning("[RpcMessageHandler] Received message without type marker");
                    return MessageHandleResult.Invalid;
                }
                
                return messageType switch
                {
                    RpcMessageType.Request => HandleRequest(data),
                    RpcMessageType.Response => HandleResponse(data),
                    _ => MessageHandleResult.Invalid
                };
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcMessageHandler] Error processing message: {ex.Message}");
                return MessageHandleResult.Error(ex);
            }
        }

        /// <summary>
        /// 处理请求消息
        /// </summary>
        private MessageHandleResult HandleRequest(byte[] data)
        {
            try
            {
                var message = _serializer.Deserialize<RpcMessage>(data);
                return MessageHandleResult.CreateRequest(message);
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcMessageHandler] Failed to deserialize RpcMessage: {ex.Message}");
                return MessageHandleResult.Error(ex);
            }
        }

        /// <summary>
        /// 处理响应消息
        /// </summary>
        private MessageHandleResult HandleResponse(byte[] data)
        {
            try
            {
                var response = _serializer.Deserialize<RpcResponse>(data);
                return MessageHandleResult.CreateResponse(response);
            }
            catch (Exception ex)
            {
                RpcLog.Error($"[RpcMessageHandler] Failed to deserialize RpcResponse: {ex.Message}");
                return MessageHandleResult.Error(ex);
            }
        }

    }

    /// <summary>
    /// 消息处理结果
    /// </summary>
    public class MessageHandleResult
    {
        public bool IsValid { get; private set; }
        public bool IsRequest => RequestMessage != null;
        public bool IsResponse => ResponseMessage != null;
        public bool IsError => Exception != null;
        
        public RpcMessage? RequestMessage { get; private set; }
        public RpcResponse? ResponseMessage { get; private set; }
        public Exception? Exception { get; private set; }

        private MessageHandleResult() { }

        public static MessageHandleResult CreateRequest(RpcMessage message)
        {
            return new MessageHandleResult
            {
                IsValid = true,
                RequestMessage = message
            };
        }

        public static MessageHandleResult CreateResponse(RpcResponse response)
        {
            return new MessageHandleResult
            {
                IsValid = true,
                ResponseMessage = response
            };
        }

        public static MessageHandleResult Error(Exception error)
        {
            return new MessageHandleResult
            {
                IsValid = false,
                Exception = error
            };
        }

        public static MessageHandleResult Invalid { get; } = new MessageHandleResult { IsValid = false };
    }
}

