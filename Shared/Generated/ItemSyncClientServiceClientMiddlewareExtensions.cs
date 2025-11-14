using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IItemSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 客户端版本 - 用于 RpcClient
    /// </summary>
    public static class ItemSyncClientServiceClientMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnRemoteItemDropped 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnRemoteItemDropped(this RpcClient client, Action<ItemDropData> handler)
        {
            return client.UseCallbackForMethod("ItemSyncClientService", "OnRemoteItemDropped", async (context, next) =>
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
        public static RpcClient UseOnRemoteItemPickedUp(this RpcClient client, Action<UInt32, string> handler)
        {
            return client.UseCallbackForMethod("ItemSyncClientService", "OnRemoteItemPickedUp", async (context, next) =>
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
