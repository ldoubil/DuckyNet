using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class CharacterSyncClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.ICharacterSyncClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public CharacterSyncClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnCharacterStateUpdate(CharacterSyncData syncData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnCharacterStateUpdate", new object[] { syncData } });
        }

        public void OnFullStateUpdate(CharacterSyncData[] allStates)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnFullStateUpdate", new object[] { allStates } });
        }

        public void OnCharacterLeft(string SteamId)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnCharacterLeft", new object[] { SteamId } });
        }

    }
}
