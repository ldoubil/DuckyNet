using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IAnimatorSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class AnimatorSyncClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnAnimatorStateUpdated 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnAnimatorStateUpdated(this RpcServer server, Action<string, AnimatorSyncData> handler)
        {
            return server.UseCallbackForMethod("AnimatorSyncClient", "OnAnimatorStateUpdated", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var steamId = (string)parameters[0];
                var animatorData = (AnimatorSyncData)parameters[1];
                handler(steamId, animatorData);
                return null;
            });
        }

    }
}
