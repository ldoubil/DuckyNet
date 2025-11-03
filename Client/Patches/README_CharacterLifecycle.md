# 单位生命周期监控系统

## 概述

本系统使用 HarmonyLib 补丁技术监控游戏中所有单位（怪物/NPC）的生命周期事件，并通过 EventBus 发布事件。

## 架构组件

### 1. Harmony 补丁

#### CharacterCreationPatch.cs
- **目标方法**: `CharacterSpawnerRoot.AddCreatedCharacter()`
- **功能**: 拦截所有单位创建
- **事件**: 发布 `CharacterSpawnedEvent`
- **特性**:
  - 自动为每个单位生成唯一 ID
  - 维护 ID 到单位的映射关系

#### CharacterDestructionPatch.cs
- **目标方法**: `UnityEngine.Object.Destroy()`
- **功能**: 拦截单位销毁
- **事件**: 发布 `CharacterDestroyedEvent`
- **特性**:
  - 自动清理 ID 映射
  - 静默处理异常，避免干扰正常流程

#### CharacterEventBridge.cs
- **订阅**: `Health.OnDead` 静态事件
- **功能**: 桥接游戏内现有事件到 EventBus
- **事件**: 发布 `CharacterDeathEvent`
- **特性**:
  - 使用反射动态订阅
  - 提供完整的死亡上下文信息

### 2. 事件定义 (CharacterLifecycleEvents.cs)

```csharp
// 单位创建
public class CharacterSpawnedEvent
{
    public object CharacterMainControl { get; }
    public GameObject GameObject { get; }
    public int CharacterId { get; }
}

// 单位销毁
public class CharacterDestroyedEvent
{
    public object CharacterMainControl { get; }
    public GameObject GameObject { get; }
    public int CharacterId { get; }
}

// 单位死亡
public class CharacterDeathEvent
{
    public object Health { get; }
    public object DamageInfo { get; }
    public object? CharacterMainControl { get; }
    public GameObject? GameObject { get; }
}
```

### 3. 管理器 (CharacterLifecycleManager.cs)

示例实现，展示如何使用这些事件：

```csharp
public class CharacterLifecycleManager : IDisposable
{
    private readonly EventSubscriberHelper _eventSubscriber;
    private readonly CharacterEventBridge _eventBridge;
    
    // 订阅所有生命周期事件
    _eventSubscriber.Subscribe<CharacterSpawnedEvent>(OnCharacterSpawned);
    _eventSubscriber.Subscribe<CharacterDestroyedEvent>(OnCharacterDestroyed);
    _eventSubscriber.Subscribe<CharacterDeathEvent>(OnCharacterDeath);
}
```

## 使用方法

### 1. 自动初始化

管理器已在 `ModBehaviour.cs` 中自动初始化：

```csharp
// 在 ModBehaviour.InitializeGameContext() 中
_characterLifecycleManager = new Core.CharacterLifecycleManager();
```

### 2. 订阅事件

在你的代码中订阅感兴趣的事件：

```csharp
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;

public class MyMonsterTracker
{
    private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
    
    public MyMonsterTracker()
    {
        _eventSubscriber.EnsureInitializedAndSubscribe();
        _eventSubscriber.Subscribe<CharacterSpawnedEvent>(OnMonsterSpawned);
    }
    
    private void OnMonsterSpawned(CharacterSpawnedEvent evt)
    {
        Debug.Log($"新怪物生成: ID={evt.CharacterId}, Name={evt.GameObject?.name}");
        
        // 你的自定义逻辑
        // - 记录到列表
        // - 同步到网络
        // - 附加追踪组件
        // 等等...
    }
}
```

### 3. 获取单位 ID

```csharp
using DuckyNet.Client.Patches;

// 通过 CharacterMainControl 获取 ID
int id = CharacterCreationPatch.GetCharacterId(characterMainControl);
```

## 事件流程

```
创建流程:
游戏调用 CharacterSpawnerRoot.AddCreatedCharacter()
    ↓
[CharacterCreationPatch] Postfix 拦截
    ↓
生成唯一 ID 并存储映射
    ↓
发布 CharacterSpawnedEvent 到 EventBus
    ↓
所有订阅者收到通知

销毁流程:
游戏调用 Object.Destroy()
    ↓
[CharacterDestructionPatch] Prefix 拦截
    ↓
检查是否为 CharacterMainControl
    ↓
发布 CharacterDestroyedEvent 到 EventBus
    ↓
清理 ID 映射
    ↓
所有订阅者收到通知

死亡流程:
Health.Hurt() 触发 Health.OnDead 静态事件
    ↓
[CharacterEventBridge] 监听到事件
    ↓
发布 CharacterDeathEvent 到 EventBus
    ↓
所有订阅者收到通知
```

## 注意事项

1. **ID 管理**: CharacterCreationPatch 会自动管理 ID，销毁时自动清理
2. **对象类型**: 事件中的对象使用 `object` 类型，避免硬依赖游戏类型
3. **异常处理**: 所有补丁都有完善的异常处理，不会影响游戏正常运行
4. **性能**: 使用 Prefix/Postfix 而非 Transpiler，性能开销最小
5. **清理**: ModBehaviour 卸载时会自动清理所有资源

## 调试

启用调试日志：

```csharp
#define DEBUG
// 或在编译器中定义 DEBUG 符号
```

这将输出详细的单位生命周期日志：
- `🟢 单位创建: ID=xxx`
- `🔴 单位销毁: ID=xxx`
- `💀 单位死亡: Name=xxx`

## 扩展示例

### 网络同步怪物

```csharp
private void OnCharacterSpawned(CharacterSpawnedEvent evt)
{
    // 只同步特定类型的怪物
    if (IsNetworkSyncMonster(evt.GameObject))
    {
        // 发送到服务器
        SendMonsterSpawnToServer(evt.CharacterId, evt.GameObject);
    }
}
```

### 怪物数量统计

```csharp
private Dictionary<string, int> _monsterCounts = new Dictionary<string, int>();

private void OnCharacterSpawned(CharacterSpawnedEvent evt)
{
    string type = evt.GameObject?.name ?? "Unknown";
    _monsterCounts[type] = _monsterCounts.GetValueOrDefault(type) + 1;
}

private void OnCharacterDestroyed(CharacterDestroyedEvent evt)
{
    string type = evt.GameObject?.name ?? "Unknown";
    if (_monsterCounts.ContainsKey(type))
        _monsterCounts[type]--;
}
```

## 相关文件

- 事件定义: `Client/Core/EventBus/Events/CharacterLifecycleEvents.cs`
- 创建补丁: `Client/Patches/CharacterCreationPatch.cs`
- 销毁补丁: `Client/Patches/CharacterDestructionPatch.cs`
- 事件桥接: `Client/Patches/CharacterEventBridge.cs`
- 管理器: `Client/Core/CharacterLifecycleManager.cs`
- 初始化: `Client/ModBehaviour.cs`

