using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// ISceneClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 客户端版本 - 用于 RpcClient
    /// </summary>
    public static class SceneClientServiceClientMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnPlayerEnteredScene 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnPlayerEnteredScene(this RpcClient client, Action<PlayerInfo, ScenelData> handler)
        {
            return client.UseCallbackForMethod("SceneClientService", "OnPlayerEnteredScene", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var playerInfo = (PlayerInfo)parameters[0];
                var scenelData = (ScenelData)parameters[1];
                handler(playerInfo, scenelData);
                return null;
            });
        }

        /// <summary>
        /// 为 OnPlayerLeftScene 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnPlayerLeftScene(this RpcClient client, Action<PlayerInfo, ScenelData> handler)
        {
            return client.UseCallbackForMethod("SceneClientService", "OnPlayerLeftScene", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var playerInfo = (PlayerInfo)parameters[0];
                var scenelData = (ScenelData)parameters[1];
                handler(playerInfo, scenelData);
                return null;
            });
        }

    }
}
