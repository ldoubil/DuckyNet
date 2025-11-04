using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class EquipmentClientServiceBroadcastProxy : DuckyNet.Shared.Services.IEquipmentClientService
    {
        private readonly object _server;
        public EquipmentClientServiceBroadcastProxy(object server) => _server = server;

        public void OnEquipmentSlotUpdated(EquipmentSlotUpdateNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IEquipmentClientService));
            method.Invoke(_server, new object[] { "OnEquipmentSlotUpdated", new object[] { notification } });
        }

        public void OnAllPlayersEquipmentReceived(AllPlayersEquipmentData allEquipmentData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IEquipmentClientService));
            method.Invoke(_server, new object[] { "OnAllPlayersEquipmentReceived", new object[] { allEquipmentData } });
        }

    }
}
