using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 玩家客户端服务实现类
    /// <para>实现 IPlayerClientService 接口，负责处理服务器向客户端发送的玩家相关事件回调</para>
    /// <para>这些方法由服务器通过 RPC 机制调用，用于通知客户端玩家状态变化和消息事件</para>
    /// <para>所有事件都会发布到全局 EventBus，实现系统间解耦通信</para>
    /// </summary>
    public class PlayerClientServiceImpl : IPlayerClientService
    {
        /// <summary>
        /// 接收聊天消息回调方法
        /// <para>由服务器调用，当有玩家发送聊天消息时，服务器会将消息转发给所有相关客户端</para>
        /// <para>此方法会记录日志并通过全局 EventBus 发布事件，供其他模块订阅处理</para>
        /// </summary>
        /// <param name="sender">发送消息的玩家信息对象，包含 SteamId、SteamName 等玩家基本信息</param>
        /// <param name="message">聊天消息的文本内容</param>
        public void OnChatMessage(PlayerInfo sender, string message)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new ChatMessageReceivedEvent(sender, message));
            }
        }

        /// <summary>
        /// 玩家加入回调方法
        /// <para>由服务器调用，当有新玩家成功登录加入游戏时触发</para>
        /// <para>此方法会记录日志并通过全局 EventBus 发布事件，供其他模块订阅处理</para>
        /// </summary>
        /// <param name="player">加入游戏的玩家信息对象，包含完整的玩家数据（SteamId、SteamName、AvatarUrl 等）</param>
        public void OnPlayerJoined(PlayerInfo player)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new PlayerJoinedEvent(player));
            }
        }

        /// <summary>
        /// 玩家离开回调方法
        /// <para>由服务器调用，当玩家登出或断开连接时触发</para>
        /// <para>此方法会记录日志并通过全局 EventBus 发布事件，供其他模块订阅处理</para>
        /// </summary>
        /// <param name="player">离开游戏的玩家信息对象，包含玩家标识信息</param>
        public void OnPlayerLeft(PlayerInfo player)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new PlayerLeftEvent(player));
            }
        }

        /// <summary>
        /// 服务器消息通知回调方法
        /// <para>由服务器调用，用于向客户端发送各种类型的服务器消息通知</para>
        /// <para>支持多种消息类型：信息、警告、错误、成功等，不同消息类型会使用不同的日志前缀</para>
        /// </summary>
        /// <param name="message">服务器发送的消息文本内容</param>
        /// <param name="messageType">消息类型枚举值，用于区分消息的严重程度和类型</param>
        public void OnServerMessage(string message, MessageType messageType)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new ServerMessageReceivedEvent(message, messageType));
            }
        }

        /// <summary>
        /// 接收其他玩家的位置同步数据回调方法
        /// <para>由服务器调用，当房间/场景内的其他玩家发送位置同步数据时触发</para>
        /// <para>此方法会解析同步数据并发布事件，供远程玩家位置更新系统处理</para>
        /// </summary>
        /// <param name="syncData">其他玩家的位置同步数据，包含位置、旋转、速度等信息</param>
        public void OnPlayerUnitySyncReceived(UnitySyncData syncData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new PlayerUnitySyncEvent(syncData.SteamId, syncData));
            }
        }
    }
}
