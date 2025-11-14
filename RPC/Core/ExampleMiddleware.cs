using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.RPC.Utils;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// 示例中间件 - 日志记录
    /// 演示如何使用 next() 调用下一个中间件
    /// </summary>
    public class LoggingMiddleware : IRpcMiddleware
    {
        public async Task InvokeAsync(RpcMiddlewareContext context, RpcMiddlewareDelegate next)
        {
            var startTime = DateTime.UtcNow;
            RpcLog.Info($"[LoggingMiddleware] 调用开始: {context.ServiceName}.{context.MethodName}");

            try
            {
                // 调用下一个中间件（next）
                // 这会继续执行中间件链中的下一个处理器
                await next(context);

                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                RpcLog.Info($"[LoggingMiddleware] 调用完成: {context.ServiceName}.{context.MethodName} (耗时: {duration}ms)");
            }
            catch (Exception ex)
            {
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                RpcLog.Error($"[LoggingMiddleware] 调用失败: {context.ServiceName}.{context.MethodName} (耗时: {duration}ms) - {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 示例中间件 - 权限验证
    /// 演示如何阻止调用继续执行
    /// </summary>
    public class AuthorizationMiddleware : IRpcMiddleware
    {
        public async Task InvokeAsync(RpcMiddlewareContext context, RpcMiddlewareDelegate next)
        {
            // 示例：检查客户端上下文
            if (context.ClientContext == null)
            {
                throw new UnauthorizedAccessException("需要客户端上下文");
            }

            // 可以在这里添加权限检查逻辑
            // if (!HasPermission(context.ClientContext, context.ServiceName, context.MethodName))
            // {
            //     throw new UnauthorizedAccessException("权限不足");
            //     // 如果不调用 next(context)，调用链会在这里停止
            // }

            // 继续执行下一个中间件
            await next(context);
        }
    }

    /// <summary>
    /// 示例中间件 - 缓存
    /// 演示如何提前返回结果，不调用 next()
    /// </summary>
    public class CacheMiddleware : IRpcMiddleware
    {
        private readonly Dictionary<string, object?> _cache = new Dictionary<string, object?>();

        public async Task InvokeAsync(RpcMiddlewareContext context, RpcMiddlewareDelegate next)
        {
            // 生成缓存键
            var cacheKey = $"{context.ServiceName}.{context.MethodName}";

            // 检查缓存
            if (_cache.TryGetValue(cacheKey, out var cachedResult))
            {
                context.Result = cachedResult;
                context.IsHandled = true;
                // 不调用 next()，直接返回缓存结果
                return;
            }

            // 调用下一个中间件
            await next(context);

            // 缓存结果
            if (context.IsHandled && context.Result != null)
            {
                _cache[cacheKey] = context.Result;
            }
        }
    }
}

