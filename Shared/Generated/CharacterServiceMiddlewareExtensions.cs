using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// ICharacterService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class CharacterServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 UpdateAppearanceAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseUpdateAppearanceAsync(this RpcServer server, Func<Byte[], Task<bool>> handler)
        {
            return server.UseCallbackForMethod("CharacterService", "UpdateAppearanceAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var appearanceData = (Byte[])parameters[0];
                return await handler(appearanceData);
            });
        }

        /// <summary>
        /// 为 GetAppearanceAsync 方法注册强类型回调
        /// </summary>
        public static RpcServer UseGetAppearanceAsync(this RpcServer server, Func<string, Task<Byte[]>> handler)
        {
            return server.UseCallbackForMethod("CharacterService", "GetAppearanceAsync", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var steamId = (string)parameters[0];
                return await handler(steamId);
            });
        }

    }
}
