using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// ICharacterClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class CharacterClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnPlayerAppearanceUpdated 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnPlayerAppearanceUpdated(this RpcServer server, Action<string, Byte[]> handler)
        {
            return server.UseCallbackForMethod("CharacterClientService", "OnPlayerAppearanceUpdated", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var steamId = (string)parameters[0];
                var appearanceData = (Byte[])parameters[1];
                handler(steamId, appearanceData);
                return null;
            });
        }

    }
}
