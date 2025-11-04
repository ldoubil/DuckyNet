using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class WeaponSyncClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IWeaponSyncClientService _impl;
        public WeaponSyncClientServiceServerDispatcher(DuckyNet.Shared.Services.IWeaponSyncClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnWeaponSlotUpdated": _impl.OnWeaponSlotUpdated((WeaponSlotUpdateNotification)args[0]); return null;
                case "OnAllPlayersWeaponReceived": _impl.OnAllPlayersWeaponReceived((AllPlayersWeaponData)args[0]); return null;
                case "OnWeaponSwitched": _impl.OnWeaponSwitched((WeaponSwitchNotification)args[0]); return null;
                case "OnWeaponFired": _impl.OnWeaponFired((WeaponFireData)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
