using DuckyNet.Shared.Data;
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
        /// 服务器消息通知
        /// </summary>
        [ServerToClient]
        void OnServerMessage(string message, MessageType messageType = MessageType.Info);

        /// <summary>
        /// 接收其他玩家的位置同步数据
        /// 服务器广播给房间/场景内的其他玩家
        /// </summary>
        [ServerToClient]
        void OnPlayerUnitySyncReceived(UnitySyncData syncData);
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
        public ScenelData CurrentScenelData { get; set; } = new ScenelData("", "");
        /// <summary>
        /// 角色是否已加载
        /// </summary>
        public bool HasCharacter { get; set; }

        /// <summary>
        /// 角色外观数据（压缩的二进制格式）
        /// </summary>
        public byte[]? AppearanceData { get; set; }

        /// <summary>
        /// 玩家装备数据（5个装备槽位的物品TypeID）
        /// </summary>
        public PlayerEquipmentData EquipmentData { get; set; } = new PlayerEquipmentData();

        /// <summary>
        /// 玩家武器数据（3个武器槽位的完整物品数据）
        /// </summary>
        public PlayerWeaponData? WeaponData { get; set; }

        /// <summary>
        /// 验证玩家信息是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(SteamId) && !string.IsNullOrWhiteSpace(SteamName);
        }

    }



}

