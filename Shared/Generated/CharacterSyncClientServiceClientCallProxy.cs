using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class CharacterSyncClientServiceClientCallProxy : DuckyNet.Shared.Services.ICharacterSyncClientService
    {
        private readonly IClientContext _client;
        public CharacterSyncClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnCharacterStateUpdate(CharacterSyncData syncData) => _client.Invoke<DuckyNet.Shared.Services.ICharacterSyncClientService>("OnCharacterStateUpdate", syncData);

        public void OnFullStateUpdate(CharacterSyncData[] allStates) => _client.Invoke<DuckyNet.Shared.Services.ICharacterSyncClientService>("OnFullStateUpdate", allStates);

        public void OnCharacterLeft(string playerId) => _client.Invoke<DuckyNet.Shared.Services.ICharacterSyncClientService>("OnCharacterLeft", playerId);

    }
}
