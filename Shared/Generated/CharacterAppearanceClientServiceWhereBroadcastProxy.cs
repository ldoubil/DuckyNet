using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class CharacterAppearanceClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.ICharacterAppearanceClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public CharacterAppearanceClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnAppearanceReceived(string steamId, CharacterAppearanceData appearanceData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ICharacterAppearanceClientService));
            method.Invoke(_server, new object[] { _predicate, "OnAppearanceReceived", new object[] { steamId, appearanceData } });
        }

    }
}
