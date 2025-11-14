using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IEquipmentService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class EquipmentServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 UpdateEquipmentSlotAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseUpdateEquipmentSlotAsync(this RpcServer server, Func<EquipmentSlotUpdateRequest, Task<bool>> handler)
        {
            return server.UseCallbackForMethod("EquipmentService", "UpdateEquipmentSlotAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var request = (EquipmentSlotUpdateRequest)parameters[0];
                return await handler(request);
            });
        }

    }
}
