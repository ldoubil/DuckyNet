using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IPlayerClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class PlayerClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnChatMessage 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnChatMessage(this RpcServer server, Action<PlayerInfo, string> handler)
        {
            return server.UseCallbackForMethod("PlayerClientService", "OnChatMessage", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var sender = (PlayerInfo)parameters[0];
                var message = (string)parameters[1];
                handler(sender, message);
                return null;
            });
        }

        /// <summary>
        /// 为 OnPlayerJoined 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnPlayerJoined(this RpcServer server, Action<PlayerInfo> handler)
        {
            return server.UseCallbackForMethod("PlayerClientService", "OnPlayerJoined", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var player = (PlayerInfo)parameters[0];
                handler(player);
                return null;
            });
        }

        /// <summary>
        /// 为 OnPlayerLeft 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnPlayerLeft(this RpcServer server, Action<PlayerInfo> handler)
        {
            return server.UseCallbackForMethod("PlayerClientService", "OnPlayerLeft", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var player = (PlayerInfo)parameters[0];
                handler(player);
                return null;
            });
        }

        /// <summary>
        /// 为 OnServerMessage 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnServerMessage(this RpcServer server, Action<string, MessageType> handler)
        {
            return server.UseCallbackForMethod("PlayerClientService", "OnServerMessage", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var message = (string)parameters[0];
                var messageType = (MessageType)parameters[1];
                handler(message, messageType);
                return null;
            });
        }

        /// <summary>
        /// 为 OnPlayerUnitySyncReceived 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnPlayerUnitySyncReceived(this RpcServer server, Action<UnitySyncData> handler)
        {
            return server.UseCallbackForMethod("PlayerClientService", "OnPlayerUnitySyncReceived", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var syncData = (UnitySyncData)parameters[0];
                handler(syncData);
                return null;
            });
        }

    }
}
