using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Context;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 方法处理函数委托 - 支持 next() 调用
    /// </summary>
    /// <param name="parameters">方法参数数组</param>
    /// <param name="clientContext">客户端上下文（如果有）</param>
    /// <param name="next">下一个处理函数，如果为 null 表示这是最后一个</param>
    /// <returns>处理结果，如果有返回值</returns>
    public delegate Task<object?> RpcMethodHandler(object?[]? parameters, IClientContext? clientContext, RpcMethodHandler? next);

    /// <summary>
    /// RPC 方法处理函数包装器 - 将普通方法包装为支持 next 的处理函数
    /// </summary>
    public static class RpcMethodHandlerWrapper
    {
        // 无参数
        public static RpcMethodHandler Wrap(Action handler)
        {
            return async (parameters, clientContext, next) =>
            {
                handler();
                if (next != null) return await next(parameters, clientContext, null);
                return null;
            };
        }

        // 1 个参数
        public static RpcMethodHandler Wrap<T1>(Action<T1> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                handler((T1)parameters![0]!);
                if (next != null) return await next(parameters, clientContext, null);
                return null;
            };
        }

        // 2 个参数
        public static RpcMethodHandler Wrap<T1, T2>(Action<T1, T2> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                handler((T1)parameters![0]!, (T2)parameters[1]!);
                if (next != null) return await next(parameters, clientContext, null);
                return null;
            };
        }

        // 3 个参数
        public static RpcMethodHandler Wrap<T1, T2, T3>(Action<T1, T2, T3> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                handler((T1)parameters![0]!, (T2)parameters[1]!, (T3)parameters[2]!);
                if (next != null) return await next(parameters, clientContext, null);
                return null;
            };
        }

        // 4 个参数
        public static RpcMethodHandler Wrap<T1, T2, T3, T4>(Action<T1, T2, T3, T4> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                handler((T1)parameters![0]!, (T2)parameters[1]!, (T3)parameters[2]!, (T4)parameters[3]!);
                if (next != null) return await next(parameters, clientContext, null);
                return null;
            };
        }

        // 有返回值 - 无参数
        public static RpcMethodHandler Wrap<TResult>(Func<TResult> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                var result = handler();
                if (next != null)
                {
                    var nextResult = await next(parameters, clientContext, null);
                    return nextResult ?? (object?)result;
                }
                return result;
            };
        }

        // 有返回值 - 1 个参数
        public static RpcMethodHandler Wrap<T1, TResult>(Func<T1, TResult> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                var result = handler((T1)parameters![0]!);
                if (next != null)
                {
                    var nextResult = await next(parameters, clientContext, null);
                    return nextResult ?? (object?)result;
                }
                return result;
            };
        }

        // 有返回值 - 2 个参数
        public static RpcMethodHandler Wrap<T1, T2, TResult>(Func<T1, T2, TResult> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                var result = handler((T1)parameters![0]!, (T2)parameters[1]!);
                if (next != null)
                {
                    var nextResult = await next(parameters, clientContext, null);
                    return nextResult ?? (object?)result;
                }
                return result;
            };
        }

        // 有返回值 - 3 个参数
        public static RpcMethodHandler Wrap<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                var result = handler((T1)parameters![0]!, (T2)parameters[1]!, (T3)parameters[2]!);
                if (next != null)
                {
                    var nextResult = await next(parameters, clientContext, null);
                    return nextResult ?? (object?)result;
                }
                return result;
            };
        }

        // 有返回值 - 4 个参数
        public static RpcMethodHandler Wrap<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, TResult> handler)
        {
            return async (parameters, clientContext, next) =>
            {
                var result = handler((T1)parameters![0]!, (T2)parameters[1]!, (T3)parameters[2]!, (T4)parameters[3]!);
                if (next != null)
                {
                    var nextResult = await next(parameters, clientContext, null);
                    return nextResult ?? (object?)result;
                }
                return result;
            };
        }
    }
}

