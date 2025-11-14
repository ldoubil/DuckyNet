using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IHealthSyncService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class HealthSyncServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 SendHealthSync 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseSendHealthSync(this RpcServer server, Action<HealthSyncData> handler)
        {
            return server.UseCallbackForMethod("HealthSyncService", "SendHealthSync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var healthData = (HealthSyncData)parameters[0];
                handler(healthData);
                return null;
            });
        }

    }
}
