using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class CharacterSyncClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.ICharacterSyncClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public CharacterSyncClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnCharacterStateUpdate(CharacterSyncData syncData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnCharacterStateUpdate", new object[] { syncData } });
        }

        public void OnFullStateUpdate(CharacterSyncData[] allStates)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnFullStateUpdate", new object[] { allStates } });
        }

        public void OnCharacterLeft(string SteamId)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnCharacterLeft", new object[] { SteamId } });
        }

    }
}
