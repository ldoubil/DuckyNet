using DuckyNet.Shared.RPC;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 玩家场景信息
    /// </summary>
    [Serializable]
    public class PlayerSceneInfo
    {
        /// <summary>
        /// Steam ID（玩家唯一标识）
        /// </summary>
        public string SteamId { get; set; } = string.Empty;

        /// <summary>
        /// 玩家信息
        /// </summary>
        public PlayerInfo? PlayerInfo { get; set; }

        /// <summary>
        /// 当前场景名称（地图名）
        /// </summary>
        public string SceneName { get; set; } = string.Empty;
    }

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
        Task<bool> EnterSceneAsync(IClientContext client, string sceneName);

        /// <summary>
        /// 离开当前场景
        /// </summary>
        [ClientToServer]
        Task<bool> LeaveSceneAsync(IClientContext client);

        /// <summary>
        /// 获取场景内的玩家列表
        /// </summary>
        [ClientToServer]
        Task<PlayerSceneInfo[]> GetScenePlayersAsync(IClientContext client, string sceneName);

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
        void OnPlayerEnteredScene(PlayerSceneInfo playerSceneInfo);

        /// <summary>
        /// 玩家离开场景通知
        /// </summary>
        [ServerToClient]
        void OnPlayerLeftScene(string SteamId, string sceneName);
    }
}

