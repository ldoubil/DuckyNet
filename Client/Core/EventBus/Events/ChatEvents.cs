using DuckyNet.Shared.Events;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 聊天消息接收事件
    /// 当服务器转发其他玩家发送的聊天消息时触发此事件
    /// </summary>
    public class ChatMessageReceivedEvent : EventBase
    {
        /// <summary>
        /// 发送消息的玩家信息
        /// </summary>
        public PlayerInfo Sender { get; }
        
        /// <summary>
        /// 聊天消息内容
        /// </summary>
        public string Message { get; }
        
        public ChatMessageReceivedEvent(PlayerInfo sender, string message)
        {
            Sender = sender;
            Message = message;
        }
    }
}
