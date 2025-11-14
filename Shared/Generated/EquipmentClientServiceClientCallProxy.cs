using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class EquipmentClientServiceClientCallProxy : DuckyNet.Shared.Services.IEquipmentClientService
    {
        private readonly IClientContext _client;
        public EquipmentClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnEquipmentSlotUpdated(EquipmentSlotUpdateNotification notification) => _client.Invoke<DuckyNet.Shared.Services.IEquipmentClientService>("OnEquipmentSlotUpdated", notification);

        public void OnAllPlayersEquipmentReceived(AllPlayersEquipmentData allEquipmentData) => _client.Invoke<DuckyNet.Shared.Services.IEquipmentClientService>("OnAllPlayersEquipmentReceived", allEquipmentData);

    }
}
