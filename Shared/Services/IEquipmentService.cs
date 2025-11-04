using DuckyNet.Shared.Data;
using DuckyNet.Shared.RPC;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 装备同步服务接口
    /// 客户端 → 服务器：更新装备槽位
    /// 服务器 → 客户端：广播装备变更、批量发送装备数据
    /// </summary>
    [RpcService("EquipmentService")]
    public interface IEquipmentService
    {
        // ==================== 客户端 → 服务器 ====================

        /// <summary>
        /// 更新单个装备槽位
        /// 客户端调用，通知服务器装备变更
        /// </summary>
        /// <param name="client">客户端上下文</param>
        /// <param name="request">装备更新请求</param>
        /// <returns>是否成功</returns>
        [ClientToServer]
        Task<bool> UpdateEquipmentSlotAsync(IClientContext client, EquipmentSlotUpdateRequest request);
    }

    /// <summary>
    /// 装备同步客户端服务接口
    /// 服务器 → 客户端的单向通知
    /// </summary>
    [RpcService("EquipmentClientService")]
    public interface IEquipmentClientService
    {
        // ==================== 服务器 → 客户端 ====================

        /// <summary>
        /// 接收其他玩家的装备槽位更新通知
        /// </summary>
        /// <param name="notification">装备更新通知</param>
        [ServerToClient]
        void OnEquipmentSlotUpdated(EquipmentSlotUpdateNotification notification);

        /// <summary>
        /// 接收所有玩家的装备数据（加入房间时）
        /// </summary>
        /// <param name="allEquipmentData">所有玩家的装备数据</param>
        [ServerToClient]
        void OnAllPlayersEquipmentReceived(AllPlayersEquipmentData allEquipmentData);
    }
}

