using System.Threading.Tasks;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.RPC;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 角色同步服务 - 客户端到服务器
    /// 客户端发送本地角色状态到服务器
    /// </summary>
    [RpcService("CharacterSyncService")]
    public interface ICharacterSyncService
    {
        /// <summary>
        /// 同步角色状态（客户端调用，发送到服务器）
        /// </summary>
        /// <param name="client">客户端上下文</param>
        /// <param name="syncData">同步数据</param>
        [ClientToServer]
        Task SyncCharacterState(IClientContext client, CharacterSyncData syncData);

        /// <summary>
        /// 请求完整的场景状态（新加入玩家用）
        /// </summary>
        /// <param name="client">客户端上下文</param>
        [ClientToServer]
        Task RequestFullState(IClientContext client);
    }

    /// <summary>
    /// 角色同步客户端服务 - 服务器到客户端
    /// 服务器广播其他玩家的状态到客户端
    /// </summary>
    [RpcService("CharacterSyncClientService")]
    public interface ICharacterSyncClientService
    {
        /// <summary>
        /// 接收其他角色的同步数据（服务器推送）
        /// </summary>
        /// <param name="syncData">同步数据</param>
        [ServerToClient]
        void OnCharacterStateUpdate(CharacterSyncData syncData);

        /// <summary>
        /// 接收完整场景状态（批量同步）
        /// </summary>
        /// <param name="allStates">所有玩家的状态</param>
        [ServerToClient]
        void OnFullStateUpdate(CharacterSyncData[] allStates);

        /// <summary>
        /// 角色离开通知
        /// </summary>
        /// <param name="SteamId">离开的玩家SteamId</param>
        [ServerToClient]
        void OnCharacterLeft(string SteamId);
    }
}

