using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IRoomClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class RoomClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnPlayerJoinedRoom 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnPlayerJoinedRoom(this RpcServer server, Action<PlayerInfo, RoomInfo> handler)
        {
            return server.UseCallbackForMethod("RoomClientService", "OnPlayerJoinedRoom", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var player = (PlayerInfo)parameters[0];
                var room = (RoomInfo)parameters[1];
                handler(player, room);
                return null;
            });
        }

        /// <summary>
        /// 为 OnPlayerLeftRoom 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnPlayerLeftRoom(this RpcServer server, Action<PlayerInfo, RoomInfo> handler)
        {
            return server.UseCallbackForMethod("RoomClientService", "OnPlayerLeftRoom", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var player = (PlayerInfo)parameters[0];
                var room = (RoomInfo)parameters[1];
                handler(player, room);
                return null;
            });
        }

        /// <summary>
        /// 为 OnKickedFromRoom 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnKickedFromRoom(this RpcServer server, Action<string> handler)
        {
            return server.UseCallbackForMethod("RoomClientService", "OnKickedFromRoom", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var reason = (string)parameters[0];
                handler(reason);
                return null;
            });
        }

    }
}
