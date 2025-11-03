using DuckyNet.Shared.Data;
using DuckyNet.Shared.RPC;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 物品同步服务接口（客户端 → 服务器）
    /// </summary>
    [RpcService("ItemSyncService")]
    public interface IItemSyncService
    {
        /// <summary>
        /// 丢弃物品
        /// 客户端通知服务器玩家丢弃了物品，服务器分配全局 DropId 并广播给其他玩家
        /// </summary>
        /// <param name="client">客户端上下文</param>
        /// <param name="dropData">丢弃数据（不含 DropId，由服务器分配）</param>
        /// <returns>服务器分配的 DropId</returns>
        [ClientToServer]
        Task<uint> DropItemAsync(IClientContext client, ItemDropData dropData);

        /// <summary>
        /// 拾取物品
        /// 客户端通知服务器玩家拾取了物品，服务器验证并广播给其他玩家销毁该物品
        /// </summary>
        /// <param name="client">客户端上下文</param>
        /// <param name="request">拾取请求</param>
        [ClientToServer]
        Task<bool> PickupItemAsync(IClientContext client, ItemPickupRequest request);
    }

    /// <summary>
    /// 物品同步客户端服务接口（服务器 → 客户端）
    /// </summary>
    [RpcService("ItemSyncClientService")]
    public interface IItemSyncClientService
    {
        /// <summary>
        /// 接收远程玩家丢弃物品的通知
        /// 服务器广播给房间内的其他玩家，让他们在本地创建该物品
        /// </summary>
        /// <param name="dropData">完整的丢弃数据（含 DropId）</param>
        [ServerToClient]
        void OnRemoteItemDropped(ItemDropData dropData);

        /// <summary>
        /// 接收远程玩家拾取物品的通知
        /// 服务器广播给房间内的其他玩家，让他们销毁本地的该物品
        /// </summary>
        /// <param name="dropId">被拾取的物品 DropId</param>
        /// <param name="pickedByPlayerId">拾取者的 SteamId</param>
        [ServerToClient]
        void OnRemoteItemPickedUp(uint dropId, string pickedByPlayerId);
    }
}

