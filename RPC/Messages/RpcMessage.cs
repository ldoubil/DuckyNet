using System;

namespace DuckyNet.RPC.Messages
{
    /// <summary>
    /// RPC 消息类型标识
    /// </summary>
    public enum RpcMessageType : byte
    {
        Request = 0x01,   // 请求消息 (RpcMessage)
        Response = 0x02   // 响应消息 (RpcResponse)
    }

    /// <summary>
    /// RPC 消息基类
    /// </summary>
    [Serializable]
    public class RpcMessage
    {
        /// <summary>
        /// 消息ID，用于请求-响应匹配
        /// </summary>
        public int MessageId { get; set; }

        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// 方法名称
        /// </summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>
        /// 方法参数（序列化后的字节数组）
        /// </summary>
        public byte[]? Parameters { get; set; }
    }

    /// <summary>
    /// RPC 响应消息
    /// </summary>
    [Serializable]
    public class RpcResponse
    {
        /// <summary>
        /// 对应的请求消息ID
        /// </summary>
        public int MessageId { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 返回值（序列化后的字节数组）
        /// </summary>
        public byte[]? Result { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}

