using DuckyNet.Shared.Data;
using DuckyNet.RPC;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Context;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 角色外观同步服务接口 (服务端接收)
    /// 处理玩家外观数据的上传、存储和分发
    /// </summary>
    [RpcService("CharacterAppearanceService")]
    public interface ICharacterAppearanceService
    {
        /// <summary>
        /// 客户端上传自己的外观数据（首次加入或更新时调用）
        /// 服务器会存储并转发给房间内所有玩家
        /// </summary>
        [ClientToServer]
        void UploadAppearance(IClientContext client, CharacterAppearanceData appearanceData);

        /// <summary>
        /// 客户端请求获取指定玩家的外观数据
        /// 用于中途加入房间时获取其他玩家的外观
        /// </summary>
        [ClientToServer]
        void RequestAppearance(IClientContext client, string targetSteamId);
    }

    /// <summary>
    /// 角色外观同步服务接口 (客户端接收)
    /// 接收服务器推送的外观数据
    /// </summary>
    [RpcService("CharacterAppearanceClientService")]
    public interface ICharacterAppearanceClientService
    {
        /// <summary>
        /// 接收玩家的外观数据
        /// 由服务器调用，通知客户端某个玩家的外观数据
        /// </summary>
        [ServerToClient]
        void OnAppearanceReceived(string steamId, CharacterAppearanceData appearanceData);
    }
}

