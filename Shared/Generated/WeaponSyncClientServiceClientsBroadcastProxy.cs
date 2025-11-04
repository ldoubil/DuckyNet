using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class WeaponSyncClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.IWeaponSyncClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public WeaponSyncClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnWeaponSlotUpdated(WeaponSlotUpdateNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnWeaponSlotUpdated", new object[] { notification } });
        }

        public void OnAllPlayersWeaponReceived(AllPlayersWeaponData allWeaponData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnAllPlayersWeaponReceived", new object[] { allWeaponData } });
        }

        public void OnWeaponSwitched(WeaponSwitchNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnWeaponSwitched", new object[] { notification } });
        }

        public void OnWeaponFired(WeaponFireData fireData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IWeaponSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnWeaponFired", new object[] { fireData } });
        }

    }
}
