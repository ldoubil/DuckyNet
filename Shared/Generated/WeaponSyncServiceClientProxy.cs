using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class WeaponSyncServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public WeaponSyncServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task<bool> EquipWeaponAsync(WeaponSlotUpdateRequest request) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IWeaponSyncService, bool>("EquipWeaponAsync", request);
        public Task<bool> UnequipWeaponAsync(WeaponSlotUnequipRequest request) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IWeaponSyncService, bool>("UnequipWeaponAsync", request);
        public Task<bool> SwitchWeaponSlotAsync(WeaponSwitchRequest request) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IWeaponSyncService, bool>("SwitchWeaponSlotAsync", request);
        public void NotifyWeaponFire(WeaponFireData fireData) => _ctx.Invoke<DuckyNet.Shared.Services.IWeaponSyncService>("NotifyWeaponFire", fireData);
        public void NotifyWeaponFireBatch(WeaponFireBatchData batchData) => _ctx.Invoke<DuckyNet.Shared.Services.IWeaponSyncService>("NotifyWeaponFireBatch", batchData);
    }
}
