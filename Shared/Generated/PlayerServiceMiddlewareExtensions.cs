using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IPlayerService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class PlayerServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 LoginAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseLoginAsync(this RpcServer server, Func<PlayerInfo, Task<LoginResult>> handler)
        {
            return server.UseCallbackForMethod("PlayerService", "LoginAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var playerInfo = (PlayerInfo)parameters[0];
                return await handler(playerInfo);
            });
        }

        /// <summary>
        /// 为 Logout 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseLogout(this RpcServer server, Action handler)
        {
            return server.UseCallbackForMethod("PlayerService", "Logout", async (context, next) =>
            {
                handler();
                return null;
            });
        }

        /// <summary>
        /// 为 SendChatMessage 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseSendChatMessage(this RpcServer server, Action<string> handler)
        {
            return server.UseCallbackForMethod("PlayerService", "SendChatMessage", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var message = (string)parameters[0];
                handler(message);
                return null;
            });
        }

        /// <summary>
        /// 为 GetAllOnlinePlayersAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseGetAllOnlinePlayersAsync(this RpcServer server, Func<Task<PlayerInfo[]>> handler)
        {
            return server.UseCallbackForMethod("PlayerService", "GetAllOnlinePlayersAsync", async (context, next) =>
            {
                return await handler();
            });
        }

        /// <summary>
        /// 为 GetCurrentRoomPlayersAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseGetCurrentRoomPlayersAsync(this RpcServer server, Func<Task<PlayerInfo[]>> handler)
        {
            return server.UseCallbackForMethod("PlayerService", "GetCurrentRoomPlayersAsync", async (context, next) =>
            {
                return await handler();
            });
        }

    }
}
