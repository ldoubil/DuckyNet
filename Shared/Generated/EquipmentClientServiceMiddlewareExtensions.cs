using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IEquipmentClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class EquipmentClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnEquipmentSlotUpdated 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnEquipmentSlotUpdated(this RpcServer server, Action<EquipmentSlotUpdateNotification> handler)
        {
            return server.UseCallbackForMethod("EquipmentClientService", "OnEquipmentSlotUpdated", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var notification = (EquipmentSlotUpdateNotification)parameters[0];
                handler(notification);
                return null;
            });
        }

        /// <summary>
        /// 为 OnAllPlayersEquipmentReceived 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnAllPlayersEquipmentReceived(this RpcServer server, Action<AllPlayersEquipmentData> handler)
        {
            return server.UseCallbackForMethod("EquipmentClientService", "OnAllPlayersEquipmentReceived", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var allEquipmentData = (AllPlayersEquipmentData)parameters[0];
                handler(allEquipmentData);
                return null;
            });
        }

    }
}
