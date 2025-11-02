using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class CharacterAppearanceServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public CharacterAppearanceServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void UploadAppearance(CharacterAppearanceData appearanceData) => _ctx.Invoke<DuckyNet.Shared.Services.ICharacterAppearanceService>("UploadAppearance", appearanceData);
        public void RequestAppearance(string targetSteamId) => _ctx.Invoke<DuckyNet.Shared.Services.ICharacterAppearanceService>("RequestAppearance", targetSteamId);
    }
}
