using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IWeaponSyncService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class WeaponSyncServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 EquipWeaponAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseEquipWeaponAsync(this RpcServer server, Func<WeaponSlotUpdateRequest, Task<bool>> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncService", "EquipWeaponAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var request = (WeaponSlotUpdateRequest)parameters[0];
                return await handler(request);
            });
        }

        /// <summary>
        /// 为 UnequipWeaponAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseUnequipWeaponAsync(this RpcServer server, Func<WeaponSlotUnequipRequest, Task<bool>> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncService", "UnequipWeaponAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var request = (WeaponSlotUnequipRequest)parameters[0];
                return await handler(request);
            });
        }

        /// <summary>
        /// 为 SwitchWeaponSlotAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseSwitchWeaponSlotAsync(this RpcServer server, Func<WeaponSwitchRequest, Task<bool>> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncService", "SwitchWeaponSlotAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var request = (WeaponSwitchRequest)parameters[0];
                return await handler(request);
            });
        }

        /// <summary>
        /// 为 NotifyWeaponFire 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseNotifyWeaponFire(this RpcServer server, Action<WeaponFireData> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncService", "NotifyWeaponFire", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var fireData = (WeaponFireData)parameters[0];
                handler(fireData);
                return null;
            });
        }

        /// <summary>
        /// 为 NotifyWeaponFireBatch 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseNotifyWeaponFireBatch(this RpcServer server, Action<WeaponFireBatchData> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncService", "NotifyWeaponFireBatch", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var batchData = (WeaponFireBatchData)parameters[0];
                handler(batchData);
                return null;
            });
        }

    }
}
