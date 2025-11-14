using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// ICharacterAppearanceService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class CharacterAppearanceServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 UploadAppearance 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseUploadAppearance(this RpcServer server, Action<CharacterAppearanceData> handler)
        {
            return server.UseCallbackForMethod("CharacterAppearanceService", "UploadAppearance", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var appearanceData = (CharacterAppearanceData)parameters[0];
                handler(appearanceData);
                return null;
            });
        }

        /// <summary>
        /// 为 RequestAppearance 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseRequestAppearance(this RpcServer server, Action<string> handler)
        {
            return server.UseCallbackForMethod("CharacterAppearanceService", "RequestAppearance", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var targetSteamId = (string)parameters[0];
                handler(targetSteamId);
                return null;
            });
        }

    }
}
