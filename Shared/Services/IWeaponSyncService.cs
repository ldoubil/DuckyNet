using DuckyNet.Shared.Data;
using DuckyNet.Shared.RPC;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 武器同步服务接口
    /// 客户端 → 服务器：更新武器槽位
    /// 服务器 → 客户端：广播武器变更、批量发送武器数据
    /// </summary>
    [RpcService("WeaponSyncService")]
    public interface IWeaponSyncService
    {
        /// <summary>
        /// 装备武器到槽位
        /// </summary>
        [ClientToServer]
        Task<bool> EquipWeaponAsync(IClientContext client, WeaponSlotUpdateRequest request);

        /// <summary>
        /// 卸下武器槽位
        /// </summary>
        [ClientToServer]
        Task<bool> UnequipWeaponAsync(IClientContext client, WeaponSlotUnequipRequest request);

        /// <summary>
        /// 切换当前武器槽位
        /// </summary>
        [ClientToServer]
        Task<bool> SwitchWeaponSlotAsync(IClientContext client, WeaponSwitchRequest request);

        /// <summary>
        /// 通知武器开火（播放特效）
        /// </summary>
        [ClientToServer]
        void NotifyWeaponFire(IClientContext client, WeaponFireData fireData);
    }

    /// <summary>
    /// 武器同步客户端服务接口
    /// 服务器 → 客户端的单向通知
    /// </summary>
    [RpcService("WeaponSyncClientService")]
    public interface IWeaponSyncClientService
    {
        /// <summary>
        /// 接收其他玩家的武器槽位更新通知
        /// </summary>
        [ServerToClient]
        void OnWeaponSlotUpdated(WeaponSlotUpdateNotification notification);

        /// <summary>
        /// 接收所有玩家的武器数据（加入房间时）
        /// </summary>
        [ServerToClient]
        void OnAllPlayersWeaponReceived(AllPlayersWeaponData allWeaponData);

        /// <summary>
        /// 接收其他玩家的武器切换通知
        /// </summary>
        [ServerToClient]
        void OnWeaponSwitched(WeaponSwitchNotification notification);

        /// <summary>
        /// 接收其他玩家的开枪特效通知
        /// </summary>
        [ServerToClient]
        void OnWeaponFired(WeaponFireData fireData);
    }
}


