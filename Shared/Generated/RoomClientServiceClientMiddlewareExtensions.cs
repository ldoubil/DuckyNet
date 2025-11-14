using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IRoomClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 客户端版本 - 用于 RpcClient
    /// </summary>
    public static class RoomClientServiceClientMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnPlayerJoinedRoom 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnPlayerJoinedRoom(this RpcClient client, Action<PlayerInfo, RoomInfo> handler)
        {
            return client.UseCallbackForMethod("RoomClientService", "OnPlayerJoinedRoom", async (context, next) =>
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
        public static RpcClient UseOnPlayerLeftRoom(this RpcClient client, Action<PlayerInfo, RoomInfo> handler)
        {
            return client.UseCallbackForMethod("RoomClientService", "OnPlayerLeftRoom", async (context, next) =>
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
        public static RpcClient UseOnKickedFromRoom(this RpcClient client, Action<string> handler)
        {
            return client.UseCallbackForMethod("RoomClientService", "OnKickedFromRoom", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var reason = (string)parameters[0];
                handler(reason);
                return null;
            });
        }

    }
}
