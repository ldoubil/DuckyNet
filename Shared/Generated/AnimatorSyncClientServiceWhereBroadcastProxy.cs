using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class AnimatorSyncClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.IAnimatorSyncClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public AnimatorSyncClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnAnimatorStateUpdated(string steamId, AnimatorSyncData animatorData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IAnimatorSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnAnimatorStateUpdated", new object[] { steamId, animatorData } });
        }

    }
}
