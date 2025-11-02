using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class CharacterAppearanceClientServiceBroadcastProxy : DuckyNet.Shared.Services.ICharacterAppearanceClientService
    {
        private readonly object _server;
        public CharacterAppearanceClientServiceBroadcastProxy(object server) => _server = server;

        public void OnAppearanceReceived(string steamId, CharacterAppearanceData appearanceData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterAppearanceClientService));
            method.Invoke(_server, new object[] { "OnAppearanceReceived", new object[] { steamId, appearanceData } });
        }

    }
}
