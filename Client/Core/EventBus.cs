using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 全局事件总线
    /// 提供统一的事件发布/订阅机制，实现系统间的解耦通信
    /// </summary>
    public class EventBus : IDisposable
    {
        private static EventBus? _instance;
        
        /// <summary>
        /// 全局实例
        /// </summary>
        public static EventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EventBus();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 事件订阅字典：事件类型 -> 订阅者列表
        /// </summary>
        private readonly Dictionary<Type, List<WeakReference>> _subscribers = new Dictionary<Type, List<WeakReference>>();

        /// <summary>
        /// 锁对象，用于线程安全
        /// </summary>
        private readonly object _lock = new object();

        private EventBus()
        {
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                {
                    _subscribers[eventType] = new List<WeakReference>();
                }

                // 使用 WeakReference 避免内存泄漏
                _subscribers[eventType].Add(new WeakReference(handler));
                UnityEngine.Debug.Log($"[EventBus] 订阅事件: {eventType.Name}, 当前订阅者数: {_subscribers[eventType].Count}");
            }
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler == null)
                return;

            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                    return;

                var subscribers = _subscribers[eventType];
                var toRemove = subscribers
                    .Where(wr => wr.IsAlive && Equals(wr.Target, handler) || !wr.IsAlive)
                    .ToList();

                foreach (var wr in toRemove)
                {
                    subscribers.Remove(wr);
                }

                // 如果没有订阅者了，移除事件类型
                if (subscribers.Count == 0)
                {
                    _subscribers.Remove(eventType);
                }
            }
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        public void Publish<TEvent>(TEvent eventData) where TEvent : class
        {
            if (eventData == null)
                return;

            var eventType = typeof(TEvent);
            List<WeakReference>? subscribers = null;

            lock (_lock)
            {
                if (!_subscribers.ContainsKey(eventType))
                {
                    UnityEngine.Debug.LogWarning($"[EventBus] 发布事件失败: {eventType.Name} - 没有订阅者");
                    return;
                }

                // 复制订阅者列表，避免在迭代时修改
                subscribers = _subscribers[eventType].ToList();
                UnityEngine.Debug.Log($"[EventBus] 发布事件: {eventType.Name}, 订阅者数: {subscribers.Count}");
            }

            // 在锁外执行回调，避免死锁
            var deadRefs = new List<WeakReference>();
            int handlerCount = 0;
            foreach (var weakRef in subscribers)
            {
                if (!weakRef.IsAlive)
                {
                    deadRefs.Add(weakRef);
                    UnityEngine.Debug.LogWarning($"[EventBus] 发现死引用订阅者");
                    continue;
                }

                var handler = weakRef.Target as Action<TEvent>;
                if (handler != null)
                {
                    try
                    {
                        handlerCount++;
                        UnityEngine.Debug.Log($"[EventBus] 调用事件处理器 #{handlerCount} ({eventType.Name})");
                        handler(eventData);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[EventBus] 事件处理异常 ({eventType.Name}): {ex.Message}");
                        UnityEngine.Debug.LogException(ex);
                    }
                }
                else
                {
                    deadRefs.Add(weakRef);
                    UnityEngine.Debug.LogWarning($"[EventBus] 订阅者已被垃圾回收");
                }
            }

            // 清理死引用
            if (deadRefs.Count > 0)
            {
                lock (_lock)
                {
                    if (_subscribers.ContainsKey(eventType))
                    {
                        foreach (var deadRef in deadRefs)
                        {
                            _subscribers[eventType].Remove(deadRef);
                        }

                        if (_subscribers[eventType].Count == 0)
                        {
                            _subscribers.Remove(eventType);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 异步发布事件（不等待完成）
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        public void PublishAsync<TEvent>(TEvent eventData) where TEvent : class
        {
            System.Threading.Tasks.Task.Run(() => Publish(eventData));
        }

        /// <summary>
        /// 获取指定事件类型的订阅者数量（仅用于调试）
        /// </summary>
        public int GetSubscriberCount<TEvent>() where TEvent : class
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                    return 0;

                return _subscribers[eventType].Count(wr => wr.IsAlive);
            }
        }

        /// <summary>
        /// 清理所有订阅（用于测试和重置）
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _subscribers.Clear();
            }
        }

        public void Dispose()
        {
            Clear();
            _instance = null;
        }
    }

    #region 事件定义


    /// <summary>
    /// 场景加载完成事件（包含子场景ID）
    /// </summary>
    public class SceneLoadedDetailEvent
    {
        public ScenelData ScenelData { get; }
        public SceneLoadedDetailEvent(ScenelData scenelData)
        {
            ScenelData = scenelData;
        }
    }



    /// <summary>
    /// 场景卸载事件（包含子场景ID）
    /// </summary>
    public class SceneUnloadingDetailEvent
    {
        public ScenelData ScenelData { get; }
        public SceneUnloadingDetailEvent(ScenelData scenelData)
        {
            ScenelData = scenelData;
        }
    }


    /// <summary>
    /// 网络通知玩家进入场景事件
    /// </summary>
    public class PlayerEnteredSceneEvent
    {
        public PlayerInfo PlayerInfo { get; }
        public ScenelData ScenelData { get; }
        public PlayerEnteredSceneEvent(PlayerInfo playerInfo, ScenelData scenelData)
        {
            PlayerInfo = playerInfo;
            ScenelData = scenelData;
        }
    }

    /// <summary>
    /// 网络通知玩家离开场景事件
    /// </summary>
    public class PlayerLeftSceneEvent
    {
        public PlayerInfo PlayerInfo { get; }
        public ScenelData ScenelData { get; }
        public PlayerLeftSceneEvent(PlayerInfo playerInfo, ScenelData scenelData)
        {
            PlayerInfo = playerInfo;
            ScenelData = scenelData;
        }
    }

    /// <summary>
    /// 网络连接成功事件
    /// </summary>
    public class NetworkConnectedEvent
    {
        public static NetworkConnectedEvent Instance { get; } = new NetworkConnectedEvent();
        private NetworkConnectedEvent() { }
    }

    /// <summary>
    /// 网络断开连接事件
    /// </summary>
    public class NetworkDisconnectedEvent
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
    public class NetworkConnectionFailedEvent
    {
        public string Reason { get; }
        public NetworkConnectionFailedEvent(string reason)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// 加入房间事件（自己的加入事件）
    /// </summary>
    public class RoomJoinedEvent
    {
        public Shared.Services.PlayerInfo Player { get; }
        public Shared.Services.RoomInfo Room { get; }
        public RoomJoinedEvent(Shared.Services.PlayerInfo player, Shared.Services.RoomInfo room)
        {
            Player = player;
            Room = room;
        }
    }

    /// <summary>
    /// 离开房间事件（自己的离开事件）
    /// </summary>
    public class RoomLeftEvent
    {
        public Shared.Services.PlayerInfo Player { get; }
        public Shared.Services.RoomInfo Room { get; }
        public RoomLeftEvent(Shared.Services.PlayerInfo player, Shared.Services.RoomInfo room)
        {
            Player = player;
            Room = room;
        }
    }

    /// <summary>
    /// 其他玩家加入房间事件
    /// </summary>
    public class PlayerJoinedRoomEvent
    {
        public Shared.Services.PlayerInfo Player { get; }
        public Shared.Services.RoomInfo Room { get; }
        public PlayerJoinedRoomEvent(Shared.Services.PlayerInfo player, Shared.Services.RoomInfo room)
        {
            Player = player;
            Room = room;
        }
    }

    /// <summary>
    /// 其他玩家离开房间事件
    /// </summary>
    public class PlayerLeftRoomEvent
    {
        public Shared.Services.PlayerInfo Player { get; }
        public Shared.Services.RoomInfo Room { get; }
        public PlayerLeftRoomEvent(Shared.Services.PlayerInfo player, Shared.Services.RoomInfo room)
        {
            Player = player;
            Room = room;
        }
    }

    /// <summary>
    /// 被踢出房间事件
    /// </summary>
    public class KickedFromRoomEvent
    {
        public string Reason { get; }
        public KickedFromRoomEvent(string reason)
        {
            Reason = reason;
        }
    }

    /// <summary>
    /// 角色创建完成事件
    /// </summary>
    public class CharacterCreatedEvent
    {
        public string SteamId { get; }
        public GameObject? Character { get; }
        public CharacterCreatedEvent(string steamId, GameObject? character)
        {
            SteamId = steamId;
            Character = character;
        }
    }

    /// <summary>
    /// 主角色创建完成事件（本地玩家角色创建完成）
    /// </summary>
    public class MainCharacterCreatedEvent
    {
        public GameObject Character { get; }
        public MainCharacterCreatedEvent(GameObject character)
        {
            Character = character;
        }
    }

    /// <summary>
    /// 玩家外观更新事件
    /// </summary>
    public class PlayerAppearanceUpdatedEvent
    {
        public string SteamId { get; }
        public byte[] AppearanceData { get; }
        public PlayerAppearanceUpdatedEvent(string steamId, byte[] appearanceData)
        {
            SteamId = steamId;
            AppearanceData = appearanceData;
        }
    }

    /// <summary>
    /// 请求启动同步事件
    /// </summary>
    public class SyncStartRequestEvent
    {
        public static SyncStartRequestEvent Instance { get; } = new SyncStartRequestEvent();
        private SyncStartRequestEvent() { }
    }

    /// <summary>
    /// 请求停止同步事件
    /// </summary>
    public class SyncStopRequestEvent
    {
        public static SyncStopRequestEvent Instance { get; } = new SyncStopRequestEvent();
        private SyncStopRequestEvent() { }
    }


    /// <summary>
    /// 创建远程角色请求事件
    /// </summary>
    public class CreateRemoteCharacterRequestEvent
    {
        public string PlayerId { get; }
        public CreateRemoteCharacterRequestEvent(string playerId)
        {
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// 远程角色已创建事件
    /// </summary>
    public class RemoteCharacterCreatedEvent
    {
        public string PlayerId { get; }
        public GameObject? Character { get; }
        public RemoteCharacterCreatedEvent(string playerId, GameObject? character)
        {
            PlayerId = playerId;
            Character = character;
        }
    }

    /// <summary>
    /// 聊天消息接收事件
    /// 当服务器转发其他玩家发送的聊天消息时触发此事件
    /// </summary>
    public class ChatMessageReceivedEvent
    {
        /// <summary>
        /// 发送消息的玩家信息
        /// </summary>
        public Shared.Services.PlayerInfo Sender { get; }
        
        /// <summary>
        /// 聊天消息内容
        /// </summary>
        public string Message { get; }
        
        public ChatMessageReceivedEvent(Shared.Services.PlayerInfo sender, string message)
        {
            Sender = sender;
            Message = message;
        }
    }

    /// <summary>
    /// 玩家加入游戏事件
    /// 当有新玩家成功登录加入游戏时触发此事件
    /// </summary>
    public class PlayerJoinedEvent
    {
        /// <summary>
        /// 加入游戏的玩家信息
        /// </summary>
        public Shared.Services.PlayerInfo Player { get; }
        
        public PlayerJoinedEvent(Shared.Services.PlayerInfo player)
        {
            Player = player;
        }
    }

    /// <summary>
    /// 玩家离开游戏事件
    /// 当玩家登出或断开连接时触发此事件
    /// </summary>
    public class PlayerLeftEvent
    {
        /// <summary>
        /// 离开游戏的玩家信息
        /// </summary>
        public Shared.Services.PlayerInfo Player { get; }
        
        public PlayerLeftEvent(Shared.Services.PlayerInfo player)
        {
            Player = player;
        }
    }

    #endregion
}
