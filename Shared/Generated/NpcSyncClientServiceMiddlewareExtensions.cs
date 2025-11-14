using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// INpcSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class NpcSyncClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnNpcSpawned 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnNpcSpawned(this RpcServer server, Action<NpcSpawnData> handler)
        {
            return server.UseCallbackForMethod("NpcSyncClientService", "OnNpcSpawned", async (context, next) =>
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
        public static RpcServer UseOnNpcBatchTransform(this RpcServer server, Action<NpcBatchTransformData> handler)
        {
            return server.UseCallbackForMethod("NpcSyncClientService", "OnNpcBatchTransform", async (context, next) =>
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
        public static RpcServer UseOnNpcDestroyed(this RpcServer server, Action<NpcDestroyData> handler)
        {
            return server.UseCallbackForMethod("NpcSyncClientService", "OnNpcDestroyed", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var destroyData = (NpcDestroyData)parameters[0];
                handler(destroyData);
                return null;
            });
        }

    }
}
