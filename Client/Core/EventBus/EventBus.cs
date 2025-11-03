using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DuckyNet.Client.Core.EventBus
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

        /// <summary>
        /// 是否启用详细日志（调试用）
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

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
                
                if (EnableVerboseLogging)
                {
                    Debug.Log($"[EventBus] 订阅事件: {eventType.Name}, 当前订阅者数: {_subscribers[eventType].Count}");
                }
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
                    .Where(wr => (wr.IsAlive && Equals(wr.Target, handler)) || !wr.IsAlive)
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
                
                if (EnableVerboseLogging)
                {
                    Debug.Log($"[EventBus] 取消订阅事件: {eventType.Name}, 剩余订阅者数: {subscribers.Count}");
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
            {
                Debug.LogWarning("[EventBus] 尝试发布空事件，已忽略");
                return;
            }

            var eventType = typeof(TEvent);
            List<WeakReference>? subscribers = null;

            lock (_lock)
            {
                if (!_subscribers.ContainsKey(eventType))
                {
                    if (EnableVerboseLogging)
                    {
                        Debug.LogWarning($"[EventBus] 发布事件失败: {eventType.Name} - 没有订阅者");
                    }
                    return;
                }

                // 复制订阅者列表，避免在迭代时修改
                subscribers = _subscribers[eventType].ToList();
                
                #if UNITY_EDITOR || DEBUG_EVENTBUS
                if (EnableVerboseLogging)
                {
                    Debug.Log($"[EventBus] 发布事件: {eventType.Name}, 订阅者数: {subscribers.Count}");
                }
                #endif
            }

            // 在锁外执行回调，避免死锁
            var deadRefs = new List<WeakReference>();
            int handlerCount = 0;
            int successCount = 0;
            
            foreach (var weakRef in subscribers)
            {
                if (!weakRef.IsAlive)
                {
                    deadRefs.Add(weakRef);
                    continue;
                }

                var handler = weakRef.Target as Action<TEvent>;
                if (handler != null)
                {
                    try
                    {
                        handlerCount++;
                        #if UNITY_EDITOR || DEBUG_EVENTBUS
                        if (EnableVerboseLogging)
                        {
                            Debug.Log($"[EventBus] 调用事件处理器 #{handlerCount} ({eventType.Name})");
                        }
                        #endif
                        handler(eventData);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EventBus] 事件处理异常 ({eventType.Name}): {ex.Message}");
                        Debug.LogException(ex);
                    }
                }
                else
                {
                    deadRefs.Add(weakRef);
                }
            }

            // 清理死引用
            if (deadRefs.Count > 0)
            {
                CleanupDeadReferences(eventType, deadRefs);
            }

            #if UNITY_EDITOR || DEBUG_EVENTBUS
            if (EnableVerboseLogging)
            {
                Debug.Log($"[EventBus] 事件 {eventType.Name} 发布完成: 成功 {successCount}/{handlerCount}, 清理死引用 {deadRefs.Count}");
            }
            #endif
        }

        /// <summary>
        /// 清理死引用
        /// </summary>
        private void CleanupDeadReferences(Type eventType, List<WeakReference> deadRefs)
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
        /// 获取所有事件类型（仅用于调试）
        /// </summary>
        public IEnumerable<Type> GetRegisteredEventTypes()
        {
            lock (_lock)
            {
                return _subscribers.Keys.ToList();
            }
        }

        /// <summary>
        /// 清理所有死引用
        /// </summary>
        public void CleanupAllDeadReferences()
        {
            lock (_lock)
            {
                var eventTypesToRemove = new List<Type>();
                
                foreach (var kvp in _subscribers)
                {
                    var deadRefs = kvp.Value.Where(wr => !wr.IsAlive).ToList();
                    foreach (var deadRef in deadRefs)
                    {
                        kvp.Value.Remove(deadRef);
                    }
                    
                    if (kvp.Value.Count == 0)
                    {
                        eventTypesToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var eventType in eventTypesToRemove)
                {
                    _subscribers.Remove(eventType);
                }
                
                if (eventTypesToRemove.Count > 0 && EnableVerboseLogging)
                {
                    Debug.Log($"[EventBus] 清理完成，移除 {eventTypesToRemove.Count} 个空事件类型");
                }
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
                Debug.Log("[EventBus] 已清空所有订阅");
            }
        }

        public void Dispose()
        {
            Clear();
            _instance = null;
        }
    }
}

