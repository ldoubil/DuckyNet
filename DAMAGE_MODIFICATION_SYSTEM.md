# 伤害修改系统

## 概述

伤害修改系统允许您在伤害应用前后拦截和修改伤害参数，通过事件系统实现解耦的伤害处理逻辑。

## 架构设计

### 核心组件

1. **DamageModificationPatch** (`Client/Patches/DamageModificationPatch.cs`)
   - 使用 Harmony 拦截 `Health.Hurt()` 方法
   - 在伤害计算前触发 `BeforeDamageAppliedEvent`
   - 在伤害应用后触发 `AfterDamageAppliedEvent`

2. **DamageEvents** (`Client/Core/EventBus/Events/DamageEvents.cs`)
   - `BeforeDamageAppliedEvent` - 伤害应用前事件（可修改）
   - `AfterDamageAppliedEvent` - 伤害应用后事件（只读）

3. **EventBus** (`Client/Core/EventBus/EventBus.cs`)
   - 全局事件总线，负责事件的发布和订阅

## 使用方法

### 1. 基本用法

```csharp
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;

public class MyDamageHandler
{
    public void Initialize()
    {
        // 订阅伤害应用前事件
        EventBus.Instance.Subscribe<BeforeDamageAppliedEvent>(OnBeforeDamageApplied);
        
        // 订阅伤害应用后事件
        EventBus.Instance.Subscribe<AfterDamageAppliedEvent>(OnAfterDamageApplied);
    }

    private void OnBeforeDamageApplied(BeforeDamageAppliedEvent evt)
    {
        // 修改伤害值
        if (evt.IsRemotePlayer)
        {
            evt.DamageValue *= 0.5f; // 减少 50% 伤害
        }
    }

    private void OnAfterDamageApplied(AfterDamageAppliedEvent evt)
    {
        // 记录伤害统计
        Debug.Log($"造成伤害: {evt.ActualDamage}, 剩余生命: {evt.RemainingHealth}");
    }
}
```

### 2. 在 ModBehaviour 中初始化

在 `Client/ModBehaviour.cs` 的 `Awake()` 方法中添加初始化代码：

```csharp
private void Awake()
{
    // ... 现有代码 ...
    
    // 初始化伤害修改监听器
    DamageModificationExample.Initialize();
    
    // 或者自定义初始化
    EventBus.Instance.Subscribe<BeforeDamageAppliedEvent>(OnBeforeDamageApplied);
}

private void OnBeforeDamageApplied(BeforeDamageAppliedEvent evt)
{
    // 自定义伤害修改逻辑
}
```

### 3. 取消订阅（可选）

```csharp
private void OnDestroy()
{
    EventBus.Instance.Unsubscribe<BeforeDamageAppliedEvent>(OnBeforeDamageApplied);
    EventBus.Instance.Unsubscribe<AfterDamageAppliedEvent>(OnAfterDamageApplied);
}
```

## BeforeDamageAppliedEvent 详解

### 只读属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Health` | `object` | Health 组件实例 |
| `OriginalDamageInfo` | `object` | 原始 DamageInfo 对象（只读参考） |
| `TargetGameObject` | `GameObject?` | 受伤角色的 GameObject |
| `TargetCharacter` | `object?` | 受伤角色的 CharacterMainControl |
| `CharacterId` | `int?` | 角色 ID（如果已注册） |
| `IsRemotePlayer` | `bool` | 是否是远程玩家 |
| `IsLocalPlayer` | `bool` | 是否是本地玩家 |

### 可修改属性

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `DamageValue` | `float` | 原始伤害值 | 基础伤害值 |
| `IgnoreArmor` | `bool` | 原始值 | 是否忽略护甲 |
| `IgnoreDifficulty` | `bool` | 原始值 | 是否忽略难度系数 |
| `CritRate` | `float` | 原始值 | 暴击率（0-1） |
| `CritDamageFactor` | `float` | 原始值 | 暴击伤害倍率 |
| `ArmorPiercing` | `float` | 原始值 | 护甲穿透 |
| `CancelDamage` | `bool` | `false` | 是否取消伤害（设为 `true` 将完全阻止伤害） |

## AfterDamageAppliedEvent 详解

### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Health` | `object` | Health 组件实例 |
| `DamageInfo` | `object` | DamageInfo 对象 |
| `TargetGameObject` | `GameObject?` | 受伤角色的 GameObject |
| `TargetCharacter` | `object?` | 受伤角色的 CharacterMainControl |
| `CharacterId` | `int?` | 角色 ID |
| `IsRemotePlayer` | `bool` | 是否是远程玩家 |
| `IsLocalPlayer` | `bool` | 是否是本地玩家 |
| `ActualDamage` | `float` | 实际造成的伤害值 |
| `RemainingHealth` | `float` | 剩余生命值 |
| `CausedDeath` | `bool` | 是否导致死亡 |

## 使用场景

### 场景 1: 玩家伤害保护

```csharp
private void OnBeforeDamageApplied(BeforeDamageAppliedEvent evt)
{
    if (evt.IsRemotePlayer)
    {
        // 远程玩家减少 30% 伤害
        evt.DamageValue *= 0.7f;
    }
}
```

### 场景 2: 完全免疫小额伤害

```csharp
private void OnBeforeDamageApplied(BeforeDamageAppliedEvent evt)
{
    if (evt.IsRemotePlayer && evt.DamageValue < 5f)
    {
        evt.CancelDamage = true; // 取消小于 5 点的伤害
    }
}
```

### 场景 3: 增加暴击率

```csharp
private void OnBeforeDamageApplied(BeforeDamageAppliedEvent evt)
{
    if (evt.IsLocalPlayer)
    {
        evt.CritRate = Mathf.Min(1f, evt.CritRate + 0.2f); // 增加 20% 暴击率
    }
}
```

### 场景 4: 伤害统计

```csharp
private Dictionary<int, float> damageStats = new Dictionary<int, float>();

private void OnAfterDamageApplied(AfterDamageAppliedEvent evt)
{
    if (evt.CharacterId.HasValue)
    {
        if (!damageStats.ContainsKey(evt.CharacterId.Value))
        {
            damageStats[evt.CharacterId.Value] = 0f;
        }
        damageStats[evt.CharacterId.Value] += evt.ActualDamage;
    }
}
```

### 场景 5: 死亡检测

```csharp
private void OnAfterDamageApplied(AfterDamageAppliedEvent evt)
{
    if (evt.CausedDeath)
    {
        Debug.Log($"角色死亡，最后伤害: {evt.ActualDamage}");
        
        // 触发死亡相关逻辑
        if (evt.IsRemotePlayer)
        {
            OnRemotePlayerDeath(evt);
        }
    }
}
```

### 场景 6: 条件伤害修改

```csharp
private void OnBeforeDamageApplied(BeforeDamageAppliedEvent evt)
{
    // 基于时间段调整伤害
    int hour = System.DateTime.Now.Hour;
    if (hour >= 22 || hour < 6) // 晚上 10 点到早上 6 点
    {
        evt.DamageValue *= 0.5f; // 减少 50% 伤害
    }
    
    // 基于剩余生命值调整
    var healthType = evt.Health.GetType();
    var currentHealthProp = AccessTools.Property(healthType, "CurrentHealth");
    var maxHealthProp = AccessTools.Property(healthType, "MaxHealth");
    
    if (currentHealthProp != null && maxHealthProp != null)
    {
        float currentHealth = (float)currentHealthProp.GetValue(evt.Health);
        float maxHealth = (float)maxHealthProp.GetValue(evt.Health);
        
        if (currentHealth / maxHealth < 0.2f) // 生命值低于 20%
        {
            evt.DamageValue *= 0.8f; // 减少 20% 伤害
        }
    }
}
```

## 插件开发

### 创建自定义伤害插件

```csharp
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;
using UnityEngine;

namespace MyPlugin
{
    public class CustomDamagePlugin
    {
        private float damageMultiplier = 1f;
        
        public void Initialize(float multiplier)
        {
            damageMultiplier = multiplier;
            EventBus.Instance.Subscribe<BeforeDamageAppliedEvent>(OnBeforeDamageApplied);
        }
        
        public void Shutdown()
        {
            EventBus.Instance.Unsubscribe<BeforeDamageAppliedEvent>(OnBeforeDamageApplied);
        }
        
        private void OnBeforeDamageApplied(BeforeDamageAppliedEvent evt)
        {
            evt.DamageValue *= damageMultiplier;
        }
    }
}
```

## 技术细节

### Harmony 补丁机制

`DamageModificationPatch` 使用 Harmony 的 Prefix 和 Postfix 模式：

- **Prefix**: 在 `Health.Hurt()` 方法执行前调用
  - 可以修改参数（使用 `ref` 关键字）
  - 返回 `false` 可以阻止原方法执行
  - 返回 `true` 允许原方法执行

- **Postfix**: 在 `Health.Hurt()` 方法执行后调用
  - 不能修改返回值或参数
  - 主要用于记录、统计等

### 事件执行流程

```
1. 游戏调用 Health.Hurt(damageInfo)
   ↓
2. DamageModificationPatch.Prefix 拦截
   ↓
3. 提取 DamageInfo 参数
   ↓
4. 创建 BeforeDamageAppliedEvent
   ↓
5. 发布事件（EventBus.Publish）
   ↓
6. 所有订阅者修改事件属性
   ↓
7. 将修改后的值应用回 DamageInfo
   ↓
8. 继续执行原方法 Health.Hurt()
   ↓
9. DamageModificationPatch.Postfix 执行
   ↓
10. 发布 AfterDamageAppliedEvent
   ↓
11. 所有订阅者处理伤害应用后逻辑
```

### 反射访问

由于游戏代码经过混淆，使用 Harmony 的反射 API 访问：

```csharp
// 访问类型
var healthType = AccessTools.TypeByName("Health");

// 访问方法
var hurtMethod = AccessTools.Method(healthType, "Hurt");

// 访问字段
var damageValueField = AccessTools.Field(damageInfoType, "damageValue");

// 访问属性
var currentHealthProp = AccessTools.Property(healthType, "CurrentHealth");
```

## 性能考虑

1. **事件订阅**: 使用 WeakReference 避免内存泄漏
2. **反射缓存**: 字段和属性访问会有性能开销，建议缓存 FieldInfo/PropertyInfo
3. **条件检查**: 在事件处理器中尽早返回，避免不必要的计算

## 调试

### 启用详细日志

```csharp
EventBus.Instance.EnableVerboseLogging = true;
```

### 检查订阅者数量

```csharp
int subscriberCount = EventBus.Instance.GetSubscriberCount<BeforeDamageAppliedEvent>();
Debug.Log($"BeforeDamageAppliedEvent 订阅者数: {subscriberCount}");
```

### 查看所有注册的事件类型

```csharp
var eventTypes = EventBus.Instance.GetRegisteredEventTypes();
foreach (var type in eventTypes)
{
    Debug.Log($"已注册事件: {type.Name}");
}
```

## 注意事项

1. **DamageInfo 是结构体**: 必须使用 `ref` 关键字才能修改
2. **线程安全**: EventBus 是线程安全的，但事件处理器应注意 Unity API 的主线程限制
3. **异常处理**: 事件处理器中的异常会被捕获并记录，不会影响其他订阅者
4. **执行顺序**: 多个订阅者的执行顺序不保证，不要依赖执行顺序

## 示例代码

完整示例请参考 `Client/Core/DamageModificationExample.cs`

## 相关文件

- `Client/Patches/DamageModificationPatch.cs` - Harmony 补丁
- `Client/Core/EventBus/Events/DamageEvents.cs` - 事件定义
- `Client/Core/EventBus/EventBus.cs` - 事件总线
- `Client/Core/DamageModificationExample.cs` - 使用示例

## 扩展建议

1. **伤害来源追踪**: 在事件中添加攻击者信息
2. **伤害类型**: 区分物理、魔法、真实伤害等
3. **伤害减免堆栈**: 支持多层伤害减免效果
4. **配置系统**: 从配置文件读取伤害倍率
5. **网络同步**: 将伤害修改同步到服务器（防作弊）

