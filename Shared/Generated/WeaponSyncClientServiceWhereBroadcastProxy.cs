using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class WeaponSyncClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.IWeaponSyncClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public WeaponSyncClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnWeaponSlotUpdated(WeaponSlotUpdateNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnWeaponSlotUpdated", new object[] { notification } });
        }

        public void OnAllPlayersWeaponReceived(AllPlayersWeaponData allWeaponData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnAllPlayersWeaponReceived", new object[] { allWeaponData } });
        }

        public void OnWeaponSwitched(WeaponSwitchNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnWeaponSwitched", new object[] { notification } });
        }

        public void OnWeaponFired(WeaponFireData fireData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnWeaponFired", new object[] { fireData } });
        }

    }
}
