using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckyNet.Client.Core.EventBus
{
    /// <summary>
    /// EventBus 订阅辅助类 - 统一管理事件订阅，减少样板代码
    /// </summary>
    public class EventSubscriberHelper : IDisposable
    {
        private EventBus? _eventBus;
        private readonly List<IDisposableSubscription> _subscriptions = new List<IDisposableSubscription>();
        private bool _isDisposed = false;

        /// <summary>
        /// 获取 EventBus 实例（自动初始化）
        /// </summary>
        public EventBus EventBus
        {
            get
            {
                if (_eventBus == null && GameContext.IsInitialized)
                {
                    _eventBus = GameContext.Instance.EventBus;
                }
                return _eventBus ?? throw new InvalidOperationException("EventBus 未初始化，GameContext 可能尚未初始化");
            }
        }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _eventBus != null;

        /// <summary>
        /// 订阅事件数量
        /// </summary>
        public int SubscriptionCount => _subscriptions.Count;

        /// <summary>
        /// 订阅事件（自动管理，如果 GameContext 未初始化则延迟订阅）
        /// </summary>
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (_isDisposed)
            {
                Debug.LogWarning("[EventSubscriberHelper] 尝试在已释放的 Helper 上订阅事件，已忽略");
                return;
            }

            if (handler == null)
            {
                Debug.LogWarning("[EventSubscriberHelper] 尝试订阅空处理器，已忽略");
                return;
            }

            try
            {
                // 如果 GameContext 已初始化，立即订阅
                if (GameContext.IsInitialized)
                {
                    var eventBus = GameContext.Instance.EventBus;
                    eventBus.Subscribe(handler);
                    _subscriptions.Add(new Subscription<TEvent>(eventBus, handler));
                    Debug.Log($"[EventSubscriberHelper] 已订阅事件: {typeof(TEvent).Name}");
                }
                else
                {
                    // 如果未初始化，先保存 handler，稍后通过 EnsureInitializedAndSubscribe 订阅
                    Debug.LogWarning($"[EventSubscriberHelper] GameContext 未初始化，已保存订阅请求 {typeof(TEvent).Name}，请稍后调用 EnsureInitializedAndSubscribe");
                    _subscriptions.Add(new PendingSubscription<TEvent>(handler));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventSubscriberHelper] 订阅事件失败 {typeof(TEvent).Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保已初始化并完成所有待处理的订阅
        /// </summary>
        public void EnsureInitializedAndSubscribe()
        {
            if (_isDisposed)
            {
                Debug.LogWarning("[EventSubscriberHelper] 尝试在已释放的 Helper 上初始化订阅，已忽略");
                return;
            }

            if (!GameContext.IsInitialized)
            {
                Debug.LogWarning("[EventSubscriberHelper] GameContext 仍未初始化，无法完成订阅");
                return;
            }

            var eventBus = GameContext.Instance.EventBus;
            _eventBus = eventBus;

            // 处理所有待处理的订阅
            int processedCount = 0;
            for (int i = _subscriptions.Count - 1; i >= 0; i--)
            {
                if (_subscriptions[i] is PendingSubscription pending)
                {
                    var subscription = pending.Subscribe(eventBus);
                    if (subscription != null)
                    {
                        _subscriptions[i] = subscription;
                        processedCount++;
                    }
                    else
                    {
                        _subscriptions.RemoveAt(i);
                    }
                }
            }

            if (processedCount > 0)
            {
                Debug.Log($"[EventSubscriberHelper] 已完成 {processedCount} 个待处理订阅");
            }
        }

        /// <summary>
        /// 确保已初始化（用于延迟初始化）
        /// </summary>
        public void EnsureInitialized()
        {
            if (_eventBus == null && GameContext.IsInitialized)
            {
                _eventBus = GameContext.Instance.EventBus;
            }
        }

        /// <summary>
        /// 取消所有订阅
        /// </summary>
        public void UnsubscribeAll()
        {
            foreach (var subscription in _subscriptions)
            {
                try
                {
                    subscription.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[EventSubscriberHelper] 取消订阅失败: {ex.Message}");
                }
            }
            _subscriptions.Clear();
            Debug.Log("[EventSubscriberHelper] 已取消所有订阅");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            UnsubscribeAll();
            _eventBus = null;
            _isDisposed = true;
        }

        // 内部接口：可取消的订阅
        private interface IDisposableSubscription : IDisposable { }

        // 抽象基类：待处理的订阅
        private abstract class PendingSubscription : IDisposableSubscription
        {
            public abstract IDisposableSubscription? Subscribe(EventBus eventBus);
            public void Dispose() { } // 待处理的订阅不需要取消
        }

        // 具体类：待处理的类型化订阅
        private class PendingSubscription<TEvent> : PendingSubscription where TEvent : class
        {
            private readonly Action<TEvent> _handler;

            public PendingSubscription(Action<TEvent> handler)
            {
                _handler = handler;
            }

            public override IDisposableSubscription? Subscribe(EventBus eventBus)
            {
                try
                {
                    eventBus.Subscribe(_handler);
                    return new Subscription<TEvent>(eventBus, _handler);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventSubscriberHelper] 延迟订阅失败 {typeof(TEvent).Name}: {ex.Message}");
                    return null;
                }
            }
        }

        // 内部类：类型化的订阅
        private class Subscription<TEvent> : IDisposableSubscription where TEvent : class
        {
            private readonly EventBus _eventBus;
            private readonly Action<TEvent> _handler;

            public Subscription(EventBus eventBus, Action<TEvent> handler)
            {
                _eventBus = eventBus;
                _handler = handler;
            }

            public void Dispose()
            {
                _eventBus.Unsubscribe(_handler);
            }
        }
    }
}

