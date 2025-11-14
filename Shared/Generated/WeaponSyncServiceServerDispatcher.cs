using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class WeaponSyncServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IWeaponSyncService _impl;
        public WeaponSyncServiceServerDispatcher(DuckyNet.Shared.Services.IWeaponSyncService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "EquipWeaponAsync": return _impl.EquipWeaponAsync(ctx, (WeaponSlotUpdateRequest)args[0]);
                case "UnequipWeaponAsync": return _impl.UnequipWeaponAsync(ctx, (WeaponSlotUnequipRequest)args[0]);
                case "SwitchWeaponSlotAsync": return _impl.SwitchWeaponSlotAsync(ctx, (WeaponSwitchRequest)args[0]);
                case "NotifyWeaponFire": _impl.NotifyWeaponFire(ctx, (WeaponFireData)args[0]); return null;
                case "NotifyWeaponFireBatch": _impl.NotifyWeaponFireBatch(ctx, (WeaponFireBatchData)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
