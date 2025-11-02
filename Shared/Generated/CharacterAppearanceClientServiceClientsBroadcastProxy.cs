using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class CharacterAppearanceClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.ICharacterAppearanceClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public CharacterAppearanceClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnAppearanceReceived(string steamId, CharacterAppearanceData appearanceData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterAppearanceClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnAppearanceReceived", new object[] { steamId, appearanceData } });
        }

    }
}
