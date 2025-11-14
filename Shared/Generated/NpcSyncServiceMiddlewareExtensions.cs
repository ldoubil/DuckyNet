using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// INpcSyncService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class NpcSyncServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 NotifyNpcSpawned 方法注册强类型回调
        /// </summary>
        public static RpcServer UseNotifyNpcSpawned(this RpcServer server, Func<NpcSpawnData, Task> handler)
        {
            return server.UseCallbackForMethod("NpcSyncService", "NotifyNpcSpawned", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var spawnData = (NpcSpawnData)parameters[0];
                await handler(spawnData);
                return null;
            });
        }

        /// <summary>
        /// 为 NotifyNpcTransform 方法注册强类型回调
        /// </summary>
        public static RpcServer UseNotifyNpcTransform(this RpcServer server, Func<NpcTransformData, Task> handler)
        {
            return server.UseCallbackForMethod("NpcSyncService", "NotifyNpcTransform", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var transformData = (NpcTransformData)parameters[0];
                await handler(transformData);
                return null;
            });
        }

        /// <summary>
        /// 为 NotifyNpcBatchTransform 方法注册强类型回调
        /// </summary>
        public static RpcServer UseNotifyNpcBatchTransform(this RpcServer server, Func<NpcBatchTransformData, Task> handler)
        {
            return server.UseCallbackForMethod("NpcSyncService", "NotifyNpcBatchTransform", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var batchData = (NpcBatchTransformData)parameters[0];
                await handler(batchData);
                return null;
            });
        }

        /// <summary>
        /// 为 NotifyNpcDestroyed 方法注册强类型回调
        /// </summary>
        public static RpcServer UseNotifyNpcDestroyed(this RpcServer server, Func<NpcDestroyData, Task> handler)
        {
            return server.UseCallbackForMethod("NpcSyncService", "NotifyNpcDestroyed", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var destroyData = (NpcDestroyData)parameters[0];
                await handler(destroyData);
                return null;
            });
        }

        /// <summary>
        /// 为 RequestSceneNpcs 方法注册强类型回调
        /// </summary>
        public static RpcServer UseRequestSceneNpcs(this RpcServer server, Func<string, string, Task<NpcSpawnData[]>> handler)
        {
            return server.UseCallbackForMethod("NpcSyncService", "RequestSceneNpcs", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var sceneName = (string)parameters[0];
                var subSceneName = (string)parameters[1];
                return await handler(sceneName, subSceneName);
            });
        }

        /// <summary>
        /// 为 RequestSingleNpc 方法注册强类型回调
        /// </summary>
        public static RpcServer UseRequestSingleNpc(this RpcServer server, Func<string, Task<NpcSpawnData>> handler)
        {
            return server.UseCallbackForMethod("NpcSyncService", "RequestSingleNpc", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var npcId = (string)parameters[0];
                return await handler(npcId);
            });
        }

    }
}
