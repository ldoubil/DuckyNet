using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class CharacterServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public CharacterServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task<bool> UpdateAppearanceAsync(Byte[] appearanceData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.ICharacterService, bool>("UpdateAppearanceAsync", appearanceData);
        public Task<Byte[]> GetAppearanceAsync(string steamId) => _ctx.InvokeAsync<DuckyNet.Shared.Services.ICharacterService, Byte[]>("GetAppearanceAsync", steamId);
    }
}
