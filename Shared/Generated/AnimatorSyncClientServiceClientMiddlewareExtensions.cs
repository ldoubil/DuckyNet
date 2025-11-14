using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IAnimatorSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 客户端版本 - 用于 RpcClient
    /// </summary>
    public static class AnimatorSyncClientServiceClientMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnAnimatorStateUpdated 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnAnimatorStateUpdated(this RpcClient client, Action<string, AnimatorSyncData> handler)
        {
            return client.UseCallbackForMethod("AnimatorSyncClient", "OnAnimatorStateUpdated", async (context, next) =>
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
