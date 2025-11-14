using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IWeaponSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 客户端版本 - 用于 RpcClient
    /// </summary>
    public static class WeaponSyncClientServiceClientMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnWeaponSlotUpdated 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnWeaponSlotUpdated(this RpcClient client, Action<WeaponSlotUpdateNotification> handler)
        {
            return client.UseCallbackForMethod("WeaponSyncClientService", "OnWeaponSlotUpdated", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var notification = (WeaponSlotUpdateNotification)parameters[0];
                handler(notification);
                return null;
            });
        }

        /// <summary>
        /// 为 OnAllPlayersWeaponReceived 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnAllPlayersWeaponReceived(this RpcClient client, Action<AllPlayersWeaponData> handler)
        {
            return client.UseCallbackForMethod("WeaponSyncClientService", "OnAllPlayersWeaponReceived", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var allWeaponData = (AllPlayersWeaponData)parameters[0];
                handler(allWeaponData);
                return null;
            });
        }

        /// <summary>
        /// 为 OnWeaponSwitched 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnWeaponSwitched(this RpcClient client, Action<WeaponSwitchNotification> handler)
        {
            return client.UseCallbackForMethod("WeaponSyncClientService", "OnWeaponSwitched", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var notification = (WeaponSwitchNotification)parameters[0];
                handler(notification);
                return null;
            });
        }

        /// <summary>
        /// 为 OnWeaponFired 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnWeaponFired(this RpcClient client, Action<WeaponFireData> handler)
        {
            return client.UseCallbackForMethod("WeaponSyncClientService", "OnWeaponFired", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var fireData = (WeaponFireData)parameters[0];
                handler(fireData);
                return null;
            });
        }

    }
}
