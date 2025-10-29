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
    public class CharacterSyncServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public CharacterSyncServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task SyncCharacterState(CharacterSyncData syncData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.ICharacterSyncService, object>("SyncCharacterState", syncData);
        public Task RequestFullState() => _ctx.InvokeAsync<DuckyNet.Shared.Services.ICharacterSyncService, object>("RequestFullState");
    }
}
