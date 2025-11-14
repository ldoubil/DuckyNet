using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IHealthSyncClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 客户端版本 - 用于 RpcClient
    /// </summary>
    public static class HealthSyncClientServiceClientMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnHealthSyncReceived 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcClient UseOnHealthSyncReceived(this RpcClient client, Action<HealthSyncData> handler)
        {
            return client.UseCallbackForMethod("HealthSyncClientService", "OnHealthSyncReceived", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var healthData = (HealthSyncData)parameters[0];
                handler(healthData);
                return null;
            });
        }

    }
}
