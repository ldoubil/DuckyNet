using DuckyNet.Shared.RPC;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 玩家服务接口
    /// 定义客户端可以调用的服务器方法
    /// </summary>
    [RpcService("PlayerService")]
    public interface IPlayerService
    {
        /// <summary>
        /// 玩家登录（连接后3秒内必须调用，否则断开连接）
        /// </summary>
        [ClientToServer]
        Task<LoginResult> LoginAsync(IClientContext client, PlayerInfo playerInfo);

        /// <summary>
        /// 玩家登出
        /// </summary>
        [ClientToServer]
        void Logout(IClientContext client);

        /// <summary>
        /// 发送聊天消息（发送到当前房间，如果不在房间则发送到全局）
        /// </summary>
        [ClientToServer]
        void SendChatMessage(IClientContext client, string message);

        /// <summary>
        /// 更新玩家状态
        /// </summary>
        [ClientToServer]
        void UpdatePlayerStatus(IClientContext client, PlayerStatus status);

        /// <summary>
        /// 获取全局在线玩家列表（所有玩家）
        /// </summary>
        [ClientToServer]
        Task<PlayerInfo[]> GetAllOnlinePlayersAsync(IClientContext client);

        /// <summary>
        /// 获取当前房间玩家列表
        /// </summary>
        [ClientToServer]
        Task<PlayerInfo[]> GetCurrentRoomPlayersAsync(IClientContext client);
    }

    /// <summary>
    /// 玩家客户端服务接口
    /// 定义服务器可以调用的客户端方法
    /// </summary>
    [RpcService("PlayerClientService")]
    public interface IPlayerClientService
    {
        /// <summary>
        /// 接收聊天消息
        /// </summary>
        [ServerToClient]
        void OnChatMessage(PlayerInfo sender, string message);

        /// <summary>
        /// 玩家加入通知
        /// </summary>
        [ServerToClient]
        void OnPlayerJoined(PlayerInfo player);

        /// <summary>
        /// 玩家离开通知
        /// </summary>
        [ServerToClient]
        void OnPlayerLeft(PlayerInfo player);

        /// <summary>
        /// 玩家状态更新通知
        /// </summary>
        [ServerToClient]
        void OnPlayerStatusChanged(PlayerInfo player, PlayerStatus status);

        /// <summary>
        /// 服务器消息通知
        /// </summary>
        [ServerToClient]
        void OnServerMessage(string message, MessageType messageType = MessageType.Info);
    }

    /// <summary>
    /// 登录结果
    /// </summary>
    [Serializable]
    public class LoginResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public PlayerInfo? PlayerInfo { get; set; }
        public string? SessionToken { get; set; }
    }

    /// <summary>
    /// 玩家状态枚举
    /// </summary>
    [Serializable]
    public enum PlayerStatus
    {
        Offline,
        Online,
        InGame,
        InLobby,
        Away,
        Busy
    }

    /// <summary>
    /// 消息类型枚举
    /// </summary>
    [Serializable]
    public enum MessageType
    {
        Info,
        Warning,
        Error,
        Success
    }

    /// <summary>
    /// 玩家信息数据类
    /// </summary>
    [Serializable]
    public class PlayerInfo
    {
        public string SteamId { get; set; } = string.Empty;
        public string SteamName { get; set; } = string.Empty;

        /// <summary>
        /// Steam 头像 URL（中等尺寸）
        /// </summary>
        public string AvatarUrl { get; set; } = string.Empty;

        /// <summary>
        /// 玩家状态
        /// </summary>
        public PlayerStatus Status { get; set; } = PlayerStatus.Offline;

        /// <summary>
        /// 是否在游戏场景中
        /// </summary>
        public bool IsInGame { get; set; }

        /// <summary>
        /// 当前场景ID
        /// </summary>
        public string CurrentSceneId { get; set; } = string.Empty;

        /// <summary>
        /// 角色是否已加载
        /// </summary>
        public bool HasCharacter { get; set; }

        /// <summary>
        /// 登录时间
        /// </summary>
        public DateTime LoginTime { get; set; }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        public DateTime LastActivityTime { get; set; }

        /// <summary>
        /// 玩家等级
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 验证玩家信息是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(SteamId) && !string.IsNullOrWhiteSpace(SteamName);
        }

        /// <summary>
        /// 更新最后活动时间
        /// </summary>
        public void UpdateActivity()
        {
            LastActivityTime = DateTime.UtcNow;
        }
    }



}

