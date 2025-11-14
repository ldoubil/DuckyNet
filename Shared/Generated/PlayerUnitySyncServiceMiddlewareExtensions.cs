using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// IPlayerUnitySyncService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class PlayerUnitySyncServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 SendPlayerUnitySync 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseSendPlayerUnitySync(this RpcServer server, Action<UnitySyncData> handler)
        {
            return server.UseCallbackForMethod("PlayerUnitySyncService", "SendPlayerUnitySync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var syncData = (UnitySyncData)parameters[0];
                handler(syncData);
                return null;
            });
        }

    }
}
