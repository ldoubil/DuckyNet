using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class EquipmentClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public EquipmentClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnEquipmentSlotUpdated(EquipmentSlotUpdateNotification notification) => _ctx.Invoke<DuckyNet.Shared.Services.IEquipmentClientService>("OnEquipmentSlotUpdated", notification);
        public void OnAllPlayersEquipmentReceived(AllPlayersEquipmentData allEquipmentData) => _ctx.Invoke<DuckyNet.Shared.Services.IEquipmentClientService>("OnAllPlayersEquipmentReceived", allEquipmentData);
    }
}
