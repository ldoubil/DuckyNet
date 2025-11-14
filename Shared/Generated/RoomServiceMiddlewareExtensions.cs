using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IRoomService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class RoomServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 GetRoomListAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseGetRoomListAsync(this RpcServer server, Func<Task<RoomInfo[]>> handler)
        {
            return server.UseCallbackForMethod("RoomService", "GetRoomListAsync", async (context, next) =>
            {
                return await handler();
            });
        }

        /// <summary>
        /// 为 CreateRoomAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseCreateRoomAsync(this RpcServer server, Func<CreateRoomRequest, Task<RoomOperationResult>> handler)
        {
            return server.UseCallbackForMethod("RoomService", "CreateRoomAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var request = (CreateRoomRequest)parameters[0];
                return await handler(request);
            });
        }

        /// <summary>
        /// 为 JoinRoomAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseJoinRoomAsync(this RpcServer server, Func<JoinRoomRequest, Task<RoomOperationResult>> handler)
        {
            return server.UseCallbackForMethod("RoomService", "JoinRoomAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var request = (JoinRoomRequest)parameters[0];
                return await handler(request);
            });
        }

        /// <summary>
        /// 为 LeaveRoomAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseLeaveRoomAsync(this RpcServer server, Func<Task<bool>> handler)
        {
            return server.UseCallbackForMethod("RoomService", "LeaveRoomAsync", async (context, next) =>
            {
                return await handler();
            });
        }

        /// <summary>
        /// 为 GetCurrentRoomAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseGetCurrentRoomAsync(this RpcServer server, Func<Task<RoomInfo>> handler)
        {
            return server.UseCallbackForMethod("RoomService", "GetCurrentRoomAsync", async (context, next) =>
            {
                return await handler();
            });
        }

        /// <summary>
        /// 为 GetRoomInfoAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseGetRoomInfoAsync(this RpcServer server, Func<string, Task<RoomInfo>> handler)
        {
            return server.UseCallbackForMethod("RoomService", "GetRoomInfoAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var roomId = (string)parameters[0];
                return await handler(roomId);
            });
        }

        /// <summary>
        /// 为 GetRoomPlayersAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseGetRoomPlayersAsync(this RpcServer server, Func<string, Task<PlayerInfo[]>> handler)
        {
            return server.UseCallbackForMethod("RoomService", "GetRoomPlayersAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var roomId = (string)parameters[0];
                return await handler(roomId);
            });
        }

        /// <summary>
        /// 为 KickPlayerAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseKickPlayerAsync(this RpcServer server, Func<string, Task<bool>> handler)
        {
            return server.UseCallbackForMethod("RoomService", "KickPlayerAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var SteamId = (string)parameters[0];
                return await handler(SteamId);
            });
        }

    }
}
