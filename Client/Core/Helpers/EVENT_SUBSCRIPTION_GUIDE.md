# EventBus 订阅最佳实践指南

## ⚠️ 重要：统一使用 EventSubscriberHelper

为了避免事件订阅者被 GC 回收（WeakReference 导致的问题），**整个 Client 项目统一使用 `EventSubscriberHelper` 来管理事件订阅**。

## ❌ 错误方式（不要直接使用 EventBus）

```csharp
// ❌ 错误：直接使用 EventBus.Subscribe
GameContext.Instance.EventBus.Subscribe<ChatMessageReceivedEvent>(OnChatMessageReceived);

// ❌ 错误：手动管理取消订阅
GameContext.Instance.EventBus.Unsubscribe<ChatMessageReceivedEvent>(OnChatMessageReceived);
```

**问题：** EventBus 使用 `WeakReference` 存储订阅者，如果没有强引用保持存活，委托会被 GC 回收，导致事件无法触发。

## ✅ 正确方式（使用 EventSubscriberHelper）

### 1. 在类中声明 EventSubscriberHelper

```csharp
public class MyManager : IDisposable
{
    private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
    
    // ... 其他字段
}
```

### 2. 在初始化时订阅事件

```csharp
public void Initialize()
{
    // 确保 GameContext 已初始化
    _eventSubscriber.EnsureInitializedAndSubscribe();
    
    // 订阅事件（自动保持强引用）
    _eventSubscriber.Subscribe<ChatMessageReceivedEvent>(OnChatMessageReceived);
    _eventSubscriber.Subscribe<PlayerJoinedEvent>(OnPlayerJoined);
    
    // ... 其他初始化逻辑
}

private void OnChatMessageReceived(ChatMessageReceivedEvent evt)
{
    // 处理事件
    Debug.Log($"收到聊天消息: {evt.Message}");
}

private void OnPlayerJoined(PlayerJoinedEvent evt)
{
    // 处理事件
    Debug.Log($"玩家加入: {evt.Player.SteamName}");
}
```

### 3. 在 Dispose 时自动清理

```csharp
public void Dispose()
{
    // EventSubscriberHelper 会自动取消所有订阅
    _eventSubscriber?.Dispose();
}
```

## 🎯 EventSubscriberHelper 的优势

1. **自动管理生命周期**：创建时订阅，Dispose 时自动取消订阅
2. **保持强引用**：防止委托被 GC 回收
3. **统一的订阅/取消逻辑**：减少重复代码
4. **延迟初始化支持**：如果 GameContext 未初始化，会自动延迟订阅
5. **异常安全**：自动捕获和记录订阅/取消订阅时的异常

## 📋 完整示例

### 实例类（大多数情况）

```csharp
using DuckyNet.Client.Core.Helpers;

public class ChatManager : IDisposable
{
    private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
    
    public void Initialize()
    {
        _eventSubscriber.EnsureInitializedAndSubscribe();
        _eventSubscriber.Subscribe<ChatMessageReceivedEvent>(OnChatReceived);
    }
    
    private void OnChatReceived(ChatMessageReceivedEvent evt)
    {
        Debug.Log($"[Chat] {evt.Sender.SteamName}: {evt.Message}");
    }
    
    public void Dispose()
    {
        _eventSubscriber?.Dispose();
    }
}
```

### 静态类（特殊情况）

```csharp
using DuckyNet.Client.Core.Helpers;

public static class GlobalHelper
{
    private static EventSubscriberHelper? _eventSubscriber;
    
    public static void StartListening()
    {
        if (_eventSubscriber == null)
        {
            _eventSubscriber = new EventSubscriberHelper();
        }
        
        _eventSubscriber.EnsureInitializedAndSubscribe();
        _eventSubscriber.Subscribe<SomeEvent>(OnEventReceived);
    }
    
    public static void StopListening()
    {
        _eventSubscriber?.Dispose();
        _eventSubscriber = null;
    }
    
    private static void OnEventReceived(SomeEvent evt)
    {
        // 处理事件
    }
}
```

## ⚡ 常见问题

### Q: 为什么我的事件处理器没有被调用？

**A:** 检查日志是否有 `[EventBus] 发现死引用订阅者` 或 `[EventBus] 订阅者已被垃圾回收`。如果有，说明没有使用 `EventSubscriberHelper`，委托被 GC 回收了。

### Q: 什么时候调用 EnsureInitializedAndSubscribe()？

**A:** 在订阅任何事件之前调用一次即可。通常在 `Initialize()` 方法开头调用。

### Q: 可以在静态类中使用 EventSubscriberHelper 吗？

**A:** 可以，但需要手动管理静态字段的生命周期（参考上面的静态类示例）。

## 🔍 项目检查清单

在提交代码前，确保：

- [ ] 所有事件订阅都使用 `EventSubscriberHelper`
- [ ] 没有直接调用 `GameContext.Instance.EventBus.Subscribe()`
- [ ] 没有直接调用 `GameContext.Instance.EventBus.Unsubscribe()`
- [ ] 所有使用 `EventSubscriberHelper` 的类都实现了 `IDisposable`
- [ ] Dispose 方法中调用了 `_eventSubscriber?.Dispose()`

## 📚 相关文件

- `Client/Core/Helpers/EventSubscriberHelper.cs` - EventSubscriberHelper 实现
- `Client/Core/EventBus.cs` - EventBus 实现（低级 API，不要直接使用）
- `Client/Core/UIManager.cs` - 使用示例
- `Client/Core/RoomManager.cs` - 使用示例
- `Client/Core/UnitManager.cs` - 使用示例

---

**最后更新**: 2025-10-31
**维护者**: DuckyNet Team

