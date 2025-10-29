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
    public class CharacterSyncClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public CharacterSyncClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnCharacterStateUpdate(CharacterSyncData syncData) => _ctx.Invoke<DuckyNet.Shared.Services.ICharacterSyncClientService>("OnCharacterStateUpdate", syncData);
        public void OnFullStateUpdate(CharacterSyncData[] allStates) => _ctx.Invoke<DuckyNet.Shared.Services.ICharacterSyncClientService>("OnFullStateUpdate", allStates);
        public void OnCharacterLeft(string playerId) => _ctx.Invoke<DuckyNet.Shared.Services.ICharacterSyncClientService>("OnCharacterLeft", playerId);
    }
}
