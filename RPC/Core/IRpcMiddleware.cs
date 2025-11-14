using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Messages;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 中间件上下文
    /// </summary>
    public class RpcMiddlewareContext
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// 方法名称
        /// </summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>
        /// 方法参数
        /// </summary>
        public object?[]? Parameters { get; set; }

        /// <summary>
        /// 客户端上下文（如果有）
        /// </summary>
        public IClientContext? ClientContext { get; set; }

        /// <summary>
        /// 调用结果
        /// </summary>
        public object? Result { get; set; }

        /// <summary>
        /// 是否已处理（如果为 true，后续中间件不会执行）
        /// </summary>
        public bool IsHandled { get; set; }
    }

    /// <summary>
    /// RPC 中间件委托
    /// </summary>
    public delegate Task RpcMiddlewareDelegate(RpcMiddlewareContext context);

    /// <summary>
    /// RPC 中间件接口
    /// </summary>
    public interface IRpcMiddleware
    {
        /// <summary>
        /// 执行中间件逻辑
        /// </summary>
        /// <param name="context">中间件上下文</param>
        /// <param name="next">下一个中间件</param>
        Task InvokeAsync(RpcMiddlewareContext context, RpcMiddlewareDelegate next);
    }
}

