using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IItemSyncService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class ItemSyncServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 DropItemAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseDropItemAsync(this RpcServer server, Func<ItemDropData, Task<UInt32>> handler)
        {
            return server.UseCallbackForMethod("ItemSyncService", "DropItemAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var dropData = (ItemDropData)parameters[0];
                return await handler(dropData);
            });
        }

        /// <summary>
        /// 为 PickupItemAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UsePickupItemAsync(this RpcServer server, Func<ItemPickupRequest, Task<bool>> handler)
        {
            return server.UseCallbackForMethod("ItemSyncService", "PickupItemAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var request = (ItemPickupRequest)parameters[0];
                return await handler(request);
            });
        }

    }
}
