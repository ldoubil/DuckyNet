using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class EquipmentClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.IEquipmentClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public EquipmentClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnEquipmentSlotUpdated(EquipmentSlotUpdateNotification notification)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IEquipmentClientService));
            method.Invoke(_server, new object[] { _predicate, "OnEquipmentSlotUpdated", new object[] { notification } });
        }

        public void OnAllPlayersEquipmentReceived(AllPlayersEquipmentData allEquipmentData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IEquipmentClientService));
            method.Invoke(_server, new object[] { _predicate, "OnAllPlayersEquipmentReceived", new object[] { allEquipmentData } });
        }

    }
}
