using System;
using System.Threading.Tasks;

namespace DuckyNet.RPC.Context
{
    /// <summary>
    /// 客户端上下文接口
    /// 在服务器端方法中传递，标识是哪个客户端发起的请求
    /// </summary>
    public interface IClientContext
    {
        /// <summary>
        /// 客户端唯一标识
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// 客户端网络端点信息
        /// </summary>
        object NetPeer { get; }

        /// <summary>
        /// 会话状态（如已认证、已断开、活跃等）
        /// </summary>
        ClientSessionState SessionState { get; }

        /// <summary>
        /// 最后心跳时间
        /// </summary>
        DateTime LastHeartbeat { get; }

        /// <summary>
        /// 是否已断线
        /// </summary>
        bool IsDisconnected { get; }

        /// <summary>
        /// 断线重连次数
        /// </summary>
        int ReconnectCount { get; }

        /// <summary>
        /// 向该客户端发送RPC调用
        /// </summary>
        void Invoke<TService>(string methodName, params object[] parameters) where TService : class;

        /// <summary>
        /// 向该客户端发送RPC调用（异步）
        /// </summary>
        Task<TResult> InvokeAsync<TService, TResult>(string methodName, params object[] parameters) where TService : class;
    }

    /// <summary>
    /// 客户端会话状态
    /// </summary>
    public enum ClientSessionState
    {
        Unknown,
        Connected,
        Authenticated,
        Disconnected,
        Reconnecting
    }
}
