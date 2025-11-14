using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IItemSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class ItemSyncClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnRemoteItemDropped 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnRemoteItemDropped(this RpcServer server, Action<ItemDropData> handler)
        {
            return server.UseCallbackForMethod("ItemSyncClientService", "OnRemoteItemDropped", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var dropData = (ItemDropData)parameters[0];
                handler(dropData);
                return null;
            });
        }

        /// <summary>
        /// 为 OnRemoteItemPickedUp 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnRemoteItemPickedUp(this RpcServer server, Action<UInt32, string> handler)
        {
            return server.UseCallbackForMethod("ItemSyncClientService", "OnRemoteItemPickedUp", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var dropId = (UInt32)parameters[0];
                var pickedByPlayerId = (string)parameters[1];
                handler(dropId, pickedByPlayerId);
                return null;
            });
        }

    }
}
