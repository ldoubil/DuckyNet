using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Context;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 处理器委托 - 支持 next() 调用
    /// </summary>
    /// <param name="parameters">方法参数</param>
    /// <param name="clientContext">客户端上下文（如果有）</param>
    /// <param name="next">下一个处理器</param>
    /// <returns>处理结果</returns>
    public delegate Task<object?> RpcHandlerDelegate(object?[]? parameters, IClientContext? clientContext, RpcHandlerDelegate? next);

    /// <summary>
    /// RPC 处理器接口 - 支持多个处理器链式执行
    /// </summary>
    public interface IRpcHandler
    {
        /// <summary>
        /// 处理 RPC 调用
        /// </summary>
        /// <param name="parameters">方法参数</param>
        /// <param name="clientContext">客户端上下文（如果有）</param>
        /// <param name="next">下一个处理器</param>
        /// <returns>处理结果</returns>
        Task<object?> HandleAsync(object?[]? parameters, IClientContext? clientContext, RpcHandlerDelegate? next);
    }
}

