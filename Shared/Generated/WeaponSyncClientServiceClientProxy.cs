using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class WeaponSyncClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public WeaponSyncClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnWeaponSlotUpdated(WeaponSlotUpdateNotification notification) => _ctx.Invoke<DuckyNet.Shared.Services.IWeaponSyncClientService>("OnWeaponSlotUpdated", notification);
        public void OnAllPlayersWeaponReceived(AllPlayersWeaponData allWeaponData) => _ctx.Invoke<DuckyNet.Shared.Services.IWeaponSyncClientService>("OnAllPlayersWeaponReceived", allWeaponData);
        public void OnWeaponSwitched(WeaponSwitchNotification notification) => _ctx.Invoke<DuckyNet.Shared.Services.IWeaponSyncClientService>("OnWeaponSwitched", notification);
        public void OnWeaponFired(WeaponFireData fireData) => _ctx.Invoke<DuckyNet.Shared.Services.IWeaponSyncClientService>("OnWeaponFired", fireData);
    }
}
