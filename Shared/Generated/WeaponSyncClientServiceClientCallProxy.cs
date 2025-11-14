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
    public class WeaponSyncClientServiceClientCallProxy : DuckyNet.Shared.Services.IWeaponSyncClientService
    {
        private readonly IClientContext _client;
        public WeaponSyncClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnWeaponSlotUpdated(WeaponSlotUpdateNotification notification) => _client.Invoke<DuckyNet.Shared.Services.IWeaponSyncClientService>("OnWeaponSlotUpdated", notification);

        public void OnAllPlayersWeaponReceived(AllPlayersWeaponData allWeaponData) => _client.Invoke<DuckyNet.Shared.Services.IWeaponSyncClientService>("OnAllPlayersWeaponReceived", allWeaponData);

        public void OnWeaponSwitched(WeaponSwitchNotification notification) => _client.Invoke<DuckyNet.Shared.Services.IWeaponSyncClientService>("OnWeaponSwitched", notification);

        public void OnWeaponFired(WeaponFireData fireData) => _client.Invoke<DuckyNet.Shared.Services.IWeaponSyncClientService>("OnWeaponFired", fireData);

    }
}
