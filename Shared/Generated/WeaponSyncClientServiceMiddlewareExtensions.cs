using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IWeaponSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class WeaponSyncClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnWeaponSlotUpdated 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnWeaponSlotUpdated(this RpcServer server, Action<WeaponSlotUpdateNotification> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncClientService", "OnWeaponSlotUpdated", async (context, next) =>
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
        public static RpcServer UseOnAllPlayersWeaponReceived(this RpcServer server, Action<AllPlayersWeaponData> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncClientService", "OnAllPlayersWeaponReceived", async (context, next) =>
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
        public static RpcServer UseOnWeaponSwitched(this RpcServer server, Action<WeaponSwitchNotification> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncClientService", "OnWeaponSwitched", async (context, next) =>
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
        public static RpcServer UseOnWeaponFired(this RpcServer server, Action<WeaponFireData> handler)
        {
            return server.UseCallbackForMethod("WeaponSyncClientService", "OnWeaponFired", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var fireData = (WeaponFireData)parameters[0];
                handler(fireData);
                return null;
            });
        }

    }
}
