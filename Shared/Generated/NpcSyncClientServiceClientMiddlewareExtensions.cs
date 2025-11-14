using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// INpcSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 客户端版本 - 用于 RpcClient
    /// </summary>
    public static class NpcSyncClientServiceClientMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnNpcSpawned 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnNpcSpawned(this RpcClient client, Action<NpcSpawnData> handler)
        {
            return client.UseCallbackForMethod("NpcSyncClientService", "OnNpcSpawned", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var spawnData = (NpcSpawnData)parameters[0];
                handler(spawnData);
                return null;
            });
        }

        /// <summary>
        /// 为 OnNpcBatchTransform 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnNpcBatchTransform(this RpcClient client, Action<NpcBatchTransformData> handler)
        {
            return client.UseCallbackForMethod("NpcSyncClientService", "OnNpcBatchTransform", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var batchData = (NpcBatchTransformData)parameters[0];
                handler(batchData);
                return null;
            });
        }

        /// <summary>
        /// 为 OnNpcDestroyed 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnNpcDestroyed(this RpcClient client, Action<NpcDestroyData> handler)
        {
            return client.UseCallbackForMethod("NpcSyncClientService", "OnNpcDestroyed", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var destroyData = (NpcDestroyData)parameters[0];
                handler(destroyData);
                return null;
            });
        }

    }
}
