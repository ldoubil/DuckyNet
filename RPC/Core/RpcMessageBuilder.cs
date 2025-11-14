using System;
using System.Reflection;
using DuckyNet.RPC.Messages;
using RpcMessage = DuckyNet.RPC.Messages.RpcMessage;
using RpcServiceAttribute = DuckyNet.RPC.Messages.RpcServiceAttribute;
using RpcSerializer = DuckyNet.RPC.Core.RpcSerializer;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 消息构建器 - 统一构建 RPC 消息
    /// </summary>
    public class RpcMessageBuilder
    {
        private readonly RpcSerializer _serializer;
        private int _nextMessageId = 1;

        public RpcMessageBuilder(RpcSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// 构建请求消息（无返回值）
        /// </summary>
        public RpcMessage BuildRequest<TService>(string methodName, params object[] parameters) where TService : class
        {
            return new RpcMessage
            {
                MessageId = GetNextMessageId(),
                ServiceName = GetServiceName(typeof(TService)),
                MethodName = methodName,
                Parameters = _serializer.SerializeParameters(parameters)
            };
        }

        /// <summary>
        /// 构建请求消息（指定消息ID，用于异步调用）
        /// </summary>
        public RpcMessage BuildRequest<TService>(int messageId, string methodName, params object[] parameters) where TService : class
        {
            return new RpcMessage
            {
                MessageId = messageId,
                ServiceName = GetServiceName(typeof(TService)),
                MethodName = methodName,
                Parameters = _serializer.SerializeParameters(parameters)
            };
        }

        /// <summary>
        /// 构建响应消息
        /// </summary>
        public RpcResponse BuildResponse(int messageId, object? result, bool success = true, string? errorMessage = null)
        {
            return new RpcResponse
            {
                MessageId = messageId,
                Success = success,
                Result = result != null ? _serializer.Serialize(result) : null,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 构建成功响应
        /// </summary>
        public RpcResponse BuildSuccessResponse(int messageId, object? result = null)
        {
            return BuildResponse(messageId, result, success: true);
        }

        /// <summary>
        /// 构建错误响应
        /// </summary>
        public RpcResponse BuildErrorResponse(int messageId, string errorMessage)
        {
            return BuildResponse(messageId, null, success: false, errorMessage);
        }

        /// <summary>
        /// 构建错误响应（从异常）
        /// </summary>
        public RpcResponse BuildErrorResponse(int messageId, Exception exception)
        {
            return BuildErrorResponse(messageId, exception.Message);
        }

        /// <summary>
        /// 获取下一个消息ID
        /// </summary>
        public int GetNextMessageId()
        {
            return _nextMessageId++;
        }

        /// <summary>
        /// 获取服务名称
        /// </summary>
        private string GetServiceName(Type serviceType)
        {
            var attr = serviceType.GetCustomAttribute<RpcServiceAttribute>();
            return attr?.ServiceName ?? serviceType.Name;
        }
    }
}

