using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class CharacterAppearanceClientServiceClientCallProxy : DuckyNet.Shared.Services.ICharacterAppearanceClientService
    {
        private readonly IClientContext _client;
        public CharacterAppearanceClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnAppearanceReceived(string steamId, CharacterAppearanceData appearanceData) => _client.Invoke<DuckyNet.Shared.Services.ICharacterAppearanceClientService>("OnAppearanceReceived", steamId, appearanceData);

    }
}
