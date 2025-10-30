using DuckyNet.Shared.Data;
using DuckyNet.Shared.RPC;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{

    /// <summary>
    /// 场景服务接口
    /// </summary>
    [RpcService("SceneService")]
    public interface ISceneService
    {
        /// <summary>
        /// 进入场景
        /// </summary>
        [ClientToServer]
        Task<bool> EnterSceneAsync(IClientContext client, ScenelData scenelData);

        /// <summary>
        /// 离开当前场景
        /// </summary>
        [ClientToServer]
        Task<bool> LeaveSceneAsync(IClientContext client, ScenelData scenelData);

        /// <summary>
        /// 获取场景内的玩家列表
        /// </summary>
        [ClientToServer]
        Task<PlayerInfo[]> GetScenePlayersAsync(IClientContext client, ScenelData scenelData);

    }

    /// <summary>
    /// 场景客户端服务接口
    /// </summary>
    [RpcService("SceneClientService")]
    public interface ISceneClientService
    {
        /// <summary>
        /// 玩家进入场景通知
        /// </summary>
        [ServerToClient]
        void OnPlayerEnteredScene(PlayerInfo playerInfo, ScenelData scenelData);

        /// <summary>
        /// 玩家离开场景通知
        /// </summary>
        [ServerToClient]
        void OnPlayerLeftScene(PlayerInfo playerInfo, ScenelData scenelData);
    }
}

