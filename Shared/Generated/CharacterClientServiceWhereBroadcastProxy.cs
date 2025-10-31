using System;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class CharacterClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.ICharacterClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public CharacterClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnPlayerAppearanceUpdated(string steamId, Byte[] appearanceData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterClientService));
            method.Invoke(_server, new object[] { _predicate, "OnPlayerAppearanceUpdated", new object[] { steamId, appearanceData } });
        }

    }
}
