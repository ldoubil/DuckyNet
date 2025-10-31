using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class CharacterClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.ICharacterClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public CharacterClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnPlayerAppearanceUpdated(string steamId, Byte[] appearanceData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnPlayerAppearanceUpdated", new object[] { steamId, appearanceData } });
        }

    }
}
