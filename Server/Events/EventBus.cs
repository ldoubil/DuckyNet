using System;
using System.Collections.Generic;

namespace DuckyNet.Server.Events
{
    /// <summary>
    /// 事件总线实现
    /// 提供线程安全的事件发布-订阅机制
    /// </summary>
    public class EventBus : IEventBus
    {
        // 事件订阅者字典：事件类型 -> 订阅者列表
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();
        private readonly object _lock = new object();

        /// <summary>
        /// 订阅事件
        /// </summary>
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                {
                    _subscribers[eventType] = new List<Delegate>();
                }
                _subscribers[eventType].Add(handler);
            }
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (_subscribers.ContainsKey(eventType))
                {
                    _subscribers[eventType].Remove(handler);
                    
                    // 如果没有订阅者了，移除事件类型
                    if (_subscribers[eventType].Count == 0)
                    {
                        _subscribers.Remove(eventType);
                    }
                }
            }
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        public void Publish<TEvent>(TEvent eventData) where TEvent : class
        {
            if (eventData == null)
                throw new ArgumentNullException(nameof(eventData));

            List<Delegate> handlers;
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                    return;

                // 复制订阅者列表，避免在回调中修改订阅者列表导致异常
                handlers = new List<Delegate>(_subscribers[eventType]);
            }

            // 在锁外执行回调，避免死锁
            foreach (var handler in handlers)
            {
                try
                {
                    ((Action<TEvent>)handler).Invoke(eventData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EventBus] 事件处理器异常: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }
    }
}

