using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DuckyNet.RPC.Core
{
    /// <summary>
    /// RPC 中间件管道
    /// </summary>
    public class RpcMiddlewarePipeline
    {
        internal readonly List<IRpcMiddleware> _middlewares = new List<IRpcMiddleware>();
        private RpcMiddlewareDelegate? _pipeline;

        /// <summary>
        /// 添加中间件
        /// </summary>
        public RpcMiddlewarePipeline Use(IRpcMiddleware middleware)
        {
            if (middleware == null)
                throw new ArgumentNullException(nameof(middleware));

            _middlewares.Add(middleware);
            _pipeline = null; // 重置管道，下次调用时重新构建
            return this;
        }

        /// <summary>
        /// 执行中间件管道
        /// </summary>
        public async Task ExecuteAsync(RpcMiddlewareContext context)
        {
            if (_pipeline == null)
            {
                _pipeline = BuildPipeline();
            }

            await _pipeline(context);
        }

        /// <summary>
        /// 构建中间件管道
        /// </summary>
        private RpcMiddlewareDelegate BuildPipeline()
        {
            if (_middlewares.Count == 0)
            {
                // 没有中间件，返回空操作
                return context => Task.CompletedTask;
            }

            // 从最后一个中间件开始，向前构建委托链
            RpcMiddlewareDelegate pipeline = context => Task.CompletedTask;

            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                var middleware = _middlewares[i];
                var next = pipeline; // 捕获当前的 pipeline
                
                pipeline = async context =>
                {
                    await middleware.InvokeAsync(context, next);
                };
            }

            return pipeline;
        }
    }
}

