using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class EquipmentClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.IEquipmentClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public EquipmentClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnEquipmentSlotUpdated(EquipmentSlotUpdateNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IEquipmentClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnEquipmentSlotUpdated", new object[] { notification } });
        }

        public void OnAllPlayersEquipmentReceived(AllPlayersEquipmentData allEquipmentData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IEquipmentClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnAllPlayersEquipmentReceived", new object[] { allEquipmentData } });
        }

    }
}
