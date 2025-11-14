using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IAnimatorSyncService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class AnimatorSyncServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 UpdateAnimatorState 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseUpdateAnimatorState(this RpcServer server, Action<AnimatorSyncData> handler)
        {
            return server.UseCallbackForMethod("AnimatorSync", "UpdateAnimatorState", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var animatorData = (AnimatorSyncData)parameters[0];
                handler(animatorData);
                return null;
            });
        }

    }
}
