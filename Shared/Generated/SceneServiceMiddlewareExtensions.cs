using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// ISceneService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class SceneServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 EnterSceneAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseEnterSceneAsync(this RpcServer server, Func<ScenelData, Task<bool>> handler)
        {
            return server.UseCallbackForMethod("SceneService", "EnterSceneAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var scenelData = (ScenelData)parameters[0];
                return await handler(scenelData);
            });
        }

        /// <summary>
        /// 为 LeaveSceneAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseLeaveSceneAsync(this RpcServer server, Func<ScenelData, Task<bool>> handler)
        {
            return server.UseCallbackForMethod("SceneService", "LeaveSceneAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var scenelData = (ScenelData)parameters[0];
                return await handler(scenelData);
            });
        }

        /// <summary>
        /// 为 GetScenePlayersAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseGetScenePlayersAsync(this RpcServer server, Func<ScenelData, Task<PlayerInfo[]>> handler)
        {
            return server.UseCallbackForMethod("SceneService", "GetScenePlayersAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var scenelData = (ScenelData)parameters[0];
                return await handler(scenelData);
            });
        }

    }
}
