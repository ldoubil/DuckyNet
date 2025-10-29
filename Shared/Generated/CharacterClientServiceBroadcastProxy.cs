using System;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class CharacterClientServiceBroadcastProxy : DuckyNet.Shared.Services.ICharacterClientService
    {
        private readonly object _server;
        public CharacterClientServiceBroadcastProxy(object server) => _server = server;

        public void OnPlayerAppearanceUpdated(string steamId, Byte[] appearanceData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterClientService));
            method.Invoke(_server, new object[] { "OnPlayerAppearanceUpdated", new object[] { steamId, appearanceData } });
        }

    }
}
