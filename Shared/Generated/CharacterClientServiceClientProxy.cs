using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class CharacterClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public CharacterClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnPlayerAppearanceUpdated(string steamId, Byte[] appearanceData) => _ctx.Invoke<DuckyNet.Shared.Services.ICharacterClientService>("OnPlayerAppearanceUpdated", steamId, appearanceData);
    }
}
