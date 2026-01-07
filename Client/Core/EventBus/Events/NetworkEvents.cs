using DuckyNet.Shared.Events;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 网络连接成功事件
    /// </summary>
    public class NetworkConnectedEvent : EventBase
    {
        public static NetworkConnectedEvent Instance { get; } = new NetworkConnectedEvent();
        private NetworkConnectedEvent() { }
    }

    /// <summary>
    /// 网络断开连接事件
    /// </summary>
    public class NetworkDisconnectedEvent : EventBase
    {
        public string Reason { get; }
        
        public NetworkDisconnectedEvent(string reason)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// 网络连接失败事件
    /// </summary>
    public class NetworkConnectionFailedEvent : EventBase
    {
        public string Reason { get; }
        
        public NetworkConnectionFailedEvent(string reason)
        {
            Reason = reason;
        }
    }
}
