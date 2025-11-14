using System;
using System.Threading.Tasks;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Data;

namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// ICharacterAppearanceClientService 中间件扩展方法 - 提供强类型的中间件注册
    /// 服务器版本 - 用于 RpcServer
    /// </summary>
    public static class CharacterAppearanceClientServiceMiddlewareExtensions
    {
        /// <summary>
        /// 为 OnAppearanceReceived 方法注册强类型回调（同步版本）
        /// </summary>
        public static RpcServer UseOnAppearanceReceived(this RpcServer server, Action<string, CharacterAppearanceData> handler)
        {
            return server.UseCallbackForMethod("CharacterAppearanceClientService", "OnAppearanceReceived", async (context, next) =>
            {
                var parameters = context.Parameters ?? Array.Empty<object>();
                var steamId = (string)parameters[0];
                var appearanceData = (CharacterAppearanceData)parameters[1];
                handler(steamId, appearanceData);
                return null;
            });
        }

    }
}
