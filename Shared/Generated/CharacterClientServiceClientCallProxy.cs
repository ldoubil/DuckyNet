using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class CharacterClientServiceClientCallProxy : DuckyNet.Shared.Services.ICharacterClientService
    {
        private readonly IClientContext _client;
        public CharacterClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnPlayerAppearanceUpdated(string steamId, Byte[] appearanceData) => _client.Invoke<DuckyNet.Shared.Services.ICharacterClientService>("OnPlayerAppearanceUpdated", steamId, appearanceData);

    }
}
