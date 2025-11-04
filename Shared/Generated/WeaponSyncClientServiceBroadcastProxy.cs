using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class WeaponSyncClientServiceBroadcastProxy : DuckyNet.Shared.Services.IWeaponSyncClientService
    {
        private readonly object _server;
        public WeaponSyncClientServiceBroadcastProxy(object server) => _server = server;

        public void OnWeaponSlotUpdated(WeaponSlotUpdateNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { "OnWeaponSlotUpdated", new object[] { notification } });
        }

        public void OnAllPlayersWeaponReceived(AllPlayersWeaponData allWeaponData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { "OnAllPlayersWeaponReceived", new object[] { allWeaponData } });
        }

        public void OnWeaponSwitched(WeaponSwitchNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { "OnWeaponSwitched", new object[] { notification } });
        }

        public void OnWeaponFired(WeaponFireData fireData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { "OnWeaponFired", new object[] { fireData } });
        }

    }
}
