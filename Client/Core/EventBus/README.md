# EventBus 模块

## 概述

EventBus 是 DuckyNet 的全局事件总线系统，提供统一的事件发布/订阅机制，实现系统间的解耦通信。

## 目录结构

```
EventBus/
├── EventBus.cs                 # 核心事件总线类
├── EventSubscriberHelper.cs    # 订阅辅助类（简化订阅管理）
├── Events/                     # 事件定义目录
│   ├── NetworkEvents.cs        # 网络相关事件
│   ├── SceneEvents.cs          # 场景相关事件
│   ├── RoomEvents.cs           # 房间相关事件
│   ├── PlayerEvents.cs         # 玩家相关事件
│   ├── CharacterEvents.cs      # 角色相关事件
│   ├── EquipmentSyncEvents.cs  # 装备同步事件
│   ├── RemoteWeaponSyncEvents.cs # 武器同步事件
│   ├── SyncEvents.cs           # 同步相关事件
│   ├── ChatEvents.cs           # 聊天相关事件
│   ├── ItemEvents.cs           # 物品同步事件
│   ├── NpcEvents.cs            # NPC 同步事件
│   └── AllEvents.cs            # 事件索引文档
└── README.md                   # 本文档
```

## 使用方法

### 1. 引用命名空间

```csharp
using DuckyNet.Client.Core.EventBus;           // EventBus 核心类
using DuckyNet.Client.Core.EventBus.Events;    // 所有事件类型
```

### 2. 订阅事件

#### 方式一：使用 EventSubscriberHelper（推荐）

```csharp
public class MyManager : IDisposable
{
    private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
    
    public MyManager()
    {
        // 确保已初始化
        _eventSubscriber.EnsureInitializedAndSubscribe();
        
        // 订阅事件
        _eventSubscriber.Subscribe<PlayerJoinedEvent>(OnPlayerJoined);
        _eventSubscriber.Subscribe<ChatMessageReceivedEvent>(OnChatMessage);
    }
    
    private void OnPlayerJoined(PlayerJoinedEvent evt)
    {
        Debug.Log($"玩家加入: {evt.Player.SteamName}");
    }
    
    private void OnChatMessage(ChatMessageReceivedEvent evt)
    {
        Debug.Log($"[{evt.Sender.SteamName}]: {evt.Message}");
    }
    
    public void Dispose()
    {
        // 自动取消所有订阅
        _eventSubscriber?.Dispose();
    }
}
```

#### 方式二：直接使用 EventBus

```csharp
public void SubscribeEvents()
{
    var eventBus = GameContext.Instance.EventBus;
    eventBus.Subscribe<PlayerJoinedEvent>(OnPlayerJoined);
}

public void UnsubscribeEvents()
{
    var eventBus = GameContext.Instance.EventBus;
    eventBus.Unsubscribe<PlayerJoinedEvent>(OnPlayerJoined);
}
```

### 3. 发布事件

```csharp
// 发布事件
GameContext.Instance.EventBus.Publish(new PlayerJoinedEvent(playerInfo));

// 异步发布（不等待完成）
GameContext.Instance.EventBus.PublishAsync(new ChatMessageReceivedEvent(sender, message));
```

## 事件分类

### 网络事件 (NetworkEvents.cs)
- `NetworkConnectedEvent` - 网络连接成功
- `NetworkDisconnectedEvent` - 网络断开连接
- `NetworkConnectionFailedEvent` - 网络连接失败

### 场景事件 (SceneEvents.cs)
- `SceneLoadedDetailEvent` - 场景加载完成
- `SceneUnloadingDetailEvent` - 场景卸载
- `PlayerEnteredSceneEvent` - 玩家进入场景
- `PlayerLeftSceneEvent` - 玩家离开场景

### 房间事件 (RoomEvents.cs)
- `RoomJoinedEvent` - 自己加入房间
- `RoomLeftEvent` - 自己离开房间
- `PlayerJoinedRoomEvent` - 其他玩家加入房间
- `PlayerLeftRoomEvent` - 其他玩家离开房间
- `KickedFromRoomEvent` - 被踢出房间

### 玩家事件 (PlayerEvents.cs)
- `PlayerJoinedEvent` - 玩家加入游戏
- `PlayerLeftEvent` - 玩家离开游戏
- `ServerMessageReceivedEvent` - 服务器消息

### 角色事件 (CharacterEvents.cs)
- `CharacterCreatedEvent` - 角色创建完成
- `MainCharacterCreatedEvent` - 主角色创建完成
- `CreateRemoteCharacterRequestEvent` - 创建远程角色请求
- `RemoteCharacterCreatedEvent` - 远程角色已创建
- `PlayerAppearanceUpdatedEvent` - 玩家外观更新
- `CharacterAppearanceReceivedEvent` - 角色外观数据接收

### 同步事件 (SyncEvents.cs)
- `SyncStartRequestEvent` - 请求启动同步
- `SyncStopRequestEvent` - 请求停止同步
- `PlayerUnitySyncEvent` - 玩家位置同步
- `RemoteAnimatorUpdateEvent` - 远程玩家动画更新

### 装备同步事件 (EquipmentSyncEvents.cs)
- `RemoteEquipmentSlotUpdatedEvent` - 远程玩家装备槽位更新
- `AllPlayersEquipmentReceivedEvent` - 批量装备数据接收

### 武器同步事件 (RemoteWeaponSyncEvents.cs)
- `RemoteWeaponSlotUpdatedEvent` - 远程玩家武器槽位更新
- `AllPlayersWeaponReceivedEvent` - 批量武器数据接收
- `RemoteWeaponSwitchedEvent` - 远程玩家武器切换
- `RemoteWeaponFiredEvent` - 远程玩家开火特效

### 聊天事件 (ChatEvents.cs)
- `ChatMessageReceivedEvent` - 聊天消息接收

### 单位生命周期事件 (CharacterLifecycleEvents.cs)
- `CharacterSpawnedEvent` - 单位（怪物/NPC）创建
- `CharacterDestroyedEvent` - 单位销毁
- `CharacterDeathEvent` - 单位死亡（生命值为0）

### 物品同步事件 (ItemEvents.cs)
- `RemoteItemDroppedEvent` - 远程物品丢弃
- `RemoteItemPickedUpEvent` - 远程物品拾取

### NPC 同步事件 (NpcEvents.cs)
- `RemoteNpcSpawnedEvent` - 远程 NPC 生成
- `RemoteNpcBatchTransformEvent` - 远程 NPC 位置更新
- `RemoteNpcDestroyedEvent` - 远程 NPC 销毁

## 设计特点

1. **解耦通信**: 通过事件总线实现系统间的松耦合通信
2. **弱引用管理**: 使用 WeakReference 避免内存泄漏
3. **线程安全**: 支持多线程环境
4. **自动清理**: EventSubscriberHelper 自动管理订阅生命周期
5. **分类组织**: 事件按功能分类到不同文件，易于维护

## 最佳实践

1. **使用 EventSubscriberHelper**: 推荐使用 EventSubscriberHelper 来管理订阅，它会自动处理取消订阅
2. **及时取消订阅**: 在对象销毁时务必取消订阅，避免内存泄漏
3. **异常处理**: 事件处理器中的异常会被捕获并记录，不会影响其他订阅者
4. **避免循环依赖**: 不要在事件处理器中发布会导致循环的事件
5. **命名规范**: 事件名称应清晰描述事件内容，使用 Event 后缀
6. **事件定义目录**: 客户端事件统一放在 `Client/Core/EventBus/Events`（或 Shared/Events）

## RPC 回调定位（网络入站适配）

RPC 回调只做“网络入站适配”，**仅负责发布 EventBus 事件**，不做业务逻辑：

- `Client/Services/*ClientServiceImpl.cs` 仅调用 `EventBus.Publish(...)`
- 业务逻辑统一在 `Client/Core/*` 中订阅 EventBus 处理

## 迁移指南

从旧的事件系统迁移：

1. 将 `using DuckyNet.Client.Core;` 改为：
   ```csharp
   using DuckyNet.Client.Core.EventBus;
   using DuckyNet.Client.Core.EventBus.Events;
   ```

2. EventBus 类型引用需要完全限定：
   ```csharp
   // 旧代码
   public EventBus EventBus { get; private set; }
   
   // 新代码
   public EventBus.EventBus EventBus { get; private set; }
   ```

3. EventSubscriberHelper 引用不需要改变，但需要添加命名空间：
   ```csharp
   using DuckyNet.Client.Core.EventBus;
   ```
