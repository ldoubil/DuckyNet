using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class CharacterSyncClientServiceBroadcastProxy : DuckyNet.Shared.Services.ICharacterSyncClientService
    {
        private readonly object _server;
        public CharacterSyncClientServiceBroadcastProxy(object server) => _server = server;

        public void OnCharacterStateUpdate(CharacterSyncData syncData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { "OnCharacterStateUpdate", new object[] { syncData } });
        }

        public void OnFullStateUpdate(CharacterSyncData[] allStates)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { "OnFullStateUpdate", new object[] { allStates } });
        }

        public void OnCharacterLeft(string SteamId)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { "OnCharacterLeft", new object[] { SteamId } });
        }

    }
}
