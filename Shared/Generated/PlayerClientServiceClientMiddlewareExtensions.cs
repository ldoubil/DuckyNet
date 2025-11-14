using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IPlayerClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 客户端版本 - 用于 RpcClient
    /// </summary>
    public static class PlayerClientServiceClientMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnChatMessage 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnChatMessage(this RpcClient client, Action<PlayerInfo, string> handler)
        {
            return client.UseCallbackForMethod("PlayerClientService", "OnChatMessage", async (context, next) =>
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
        public static RpcClient UseOnPlayerJoined(this RpcClient client, Action<PlayerInfo> handler)
        {
            return client.UseCallbackForMethod("PlayerClientService", "OnPlayerJoined", async (context, next) =>
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
        public static RpcClient UseOnPlayerLeft(this RpcClient client, Action<PlayerInfo> handler)
        {
            return client.UseCallbackForMethod("PlayerClientService", "OnPlayerLeft", async (context, next) =>
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
        public static RpcClient UseOnServerMessage(this RpcClient client, Action<string, MessageType> handler)
        {
            return client.UseCallbackForMethod("PlayerClientService", "OnServerMessage", async (context, next) =>
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
        public static RpcClient UseOnPlayerUnitySyncReceived(this RpcClient client, Action<UnitySyncData> handler)
        {
            return client.UseCallbackForMethod("PlayerClientService", "OnPlayerUnitySyncReceived", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var syncData = (UnitySyncData)parameters[0];
                handler(syncData);
                return null;
            });
        }

    }
}
