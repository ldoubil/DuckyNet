# 武器同步系统完整实现文档

## 📋 系统概述

武器同步系统负责在客户端和服务器之间同步玩家的武器装备状态。与装备同步不同，武器同步需要传输完整的物品数据（包括配件、弹药、变量等），以保证武器在远程玩家端正确显示。

## 🏗️ 架构设计

### 核心组件

1. **Harmony 补丁系统** (`WeaponSlotPatch.cs`)
   - 拦截 `Slot.Plug()` - 武器装备事件
   - 拦截 `Slot.Unplug()` - 武器卸下事件
   - 仅捕获本地玩家的武器变更

2. **事件系统** (`WeaponSyncEvents.cs`)
   - `WeaponSlotChangedEvent` - 武器槽位变更事件
   - 支持3个槽位：主武器、副武器、近战武器

3. **数据结构** (`WeaponSyncData.cs`)
   - `WeaponSlotType` - 槽位类型枚举
   - `WeaponItemData` - 单个武器的完整数据
   - `PlayerWeaponData` - 玩家的所有武器数据
   - `WeaponSlotUpdateRequest` - 客户端请求
   - `WeaponSlotUpdateNotification` - 服务器通知
   - `AllPlayersWeaponData` - 批量数据

4. **序列化系统** (`WeaponSyncHelper.cs`)
   - 完整物品树序列化（ItemTreeData）
   - Base64 压缩编码
   - 增量优化（默认物品不传输数据）

5. **RPC 服务**
   - `IWeaponSyncService` - 服务器端接口
   - `IWeaponSyncClientService` - 客户端接口

6. **服务实现**
   - `WeaponSyncServerServiceImpl` - 服务器端逻辑
   - `WeaponSyncClientServiceImpl` - 客户端逻辑

## 📊 数据流

### 1. 玩家装备武器

```
┌─────────────┐                ┌──────────────┐                ┌─────────────┐
│  游戏本体   │                │    补丁层    │                │   事件总线  │
└─────────────┘                └──────────────┘                └─────────────┘
      │                              │                              │
      │ Slot.Plug(Item)              │                              │
      ├─────────────────────────────>│                              │
      │                              │ IsMainCharacter?             │
      │                              │ ✅                           │
      │                              │                              │
      │                              │ PublishWeaponSlotEvent()     │
      │                              ├─────────────────────────────>│
      │                              │                              │
      │                              │ SendWeaponUpdateToServer()   │
      │                              ├───────────┐                  │
      │                              │           ▼                  │
      │                              │    ┌────────────────┐        │
      │                              │    │ WeaponService  │        │
      │                              │    │ ClientProxy    │        │
      │                              │    └────────────────┘        │
      │                              │           │                  │
      │                              │           ▼                  │
      │                              │    ┌────────────────┐        │
      │                              │    │  序列化武器    │        │
      │                              │    │  (含配件等)    │        │
      │                              │    └────────────────┘        │
      │                              │           │                  │
      │                              │           ▼                  │
      │                              │    EquipWeaponAsync()        │
      │                              │    ──────────────────>       │
      │                              │                        服务器│
```

### 2. 服务器广播

```
┌──────────────┐                ┌────────────────────┐          ┌─────────────┐
│  服务器端    │                │   RoomService      │          │  其他客户端 │
└──────────────┘                └────────────────────┘          └─────────────┘
      │                                  │                            │
      │ EquipWeaponAsync()               │                            │
      ├─────────────────────────────────>│                            │
      │                                  │                            │
      │ 1. 更新 PlayerInfo.WeaponData    │                            │
      │ 2. 持久化存储                     │                            │
      │                                  │                            │
      │ BroadcastWeaponUpdate()          │                            │
      ├──────────────────────────────────┼──────────────────────────>│
      │                                  │                            │
      │                                  │ OnWeaponSlotUpdated()      │
      │                                  │                            ├──>缓存
      │                                  │                            │
      │                                  │ ApplyWeaponToCharacter()   │
      │                                  │                            ├──>应用
```

### 3. 新玩家加入房间

```
┌──────────────┐                ┌────────────────────┐          ┌─────────────┐
│  新玩家      │                │   RoomService      │          │  服务器     │
└──────────────┘                └────────────────────┘          └─────────────┘
      │                                  │                            │
      │ JoinRoomAsync()                  │                            │
      ├─────────────────────────────────>│                            │
      │                                  │                            │
      │                                  │ SendAllWeaponData()        │
      │                                  ├───────────────────────────>│
      │                                  │                            │
      │                                  │ ┌─ 遍历房间所有玩家      │
      │                                  │ ├─ 提取 WeaponData       │
      │                                  │ └─ 打包发送              │
      │                                  │                            │
      │ OnAllPlayersWeaponReceived()     │                            │
      │<─────────────────────────────────┼────────────────────────────┤
      │                                  │                            │
      │ 1. 保存到 RemotePlayer._weaponData                            │
      │ 2. 如果角色已创建，立即应用                                     │
```

## 🔧 关键实现细节

### 1. 武器槽位识别

```csharp
private static readonly int PrimWeaponSlotHash = "PrimaryWeapon".GetHashCode();
private static readonly int SecWeaponSlotHash = "SecondaryWeapon".GetHashCode();
private static readonly int MeleeWeaponSlotHash = "MeleeWeapon".GetHashCode();
```

### 2. 本地玩家过滤

```csharp
private static bool IsMainCharacterSlot(Slot slot)
{
    var characterItem = slot.Master;
    var mainCharacter = LevelManager.Instance?.MainCharacter;
    return characterItem == mainCharacter?.CharacterItem;
}
```

### 3. 增量同步优化

```csharp
// 检查是否为默认物品（没有配件、弹药等修改）
if (IsDefaultItem(weaponItem))
{
    request.IsDefaultItem = true;
    request.ItemDataCompressed = string.Empty; // 不传输数据
}
else
{
    request.IsDefaultItem = false;
    request.ItemDataCompressed = SerializeAndCompressItem(weaponItem);
}
```

### 4. 完整物品树序列化

使用 `ItemTreeData` 获取完整物品树：
- 主武器本身
- 槽位内容（配件：瞄具、握把、弹匣等）
- 库存物品（弹药）
- 自定义变量（耐久度、特殊属性等）

### 5. 延迟应用机制

```csharp
// 角色创建时延迟2帧应用武器，确保角色模型已初始化
private System.Collections.IEnumerator ApplyCachedEquipmentAndWeaponsDelayed()
{
    yield return null;
    yield return null;
    
    ApplyCachedEquipment();
    ApplyCachedWeapons();
}
```

## 📁 文件清单

### Shared (共享)
- `Shared/Data/WeaponSyncData.cs` - 数据结构定义
- `Shared/Services/IWeaponSyncService.cs` - RPC 接口定义
- `Shared/Services/IPlayerService.cs` - 扩展 PlayerInfo 添加 WeaponData 字段

### Client (客户端)
- `Client/Patches/WeaponSlotPatch.cs` - Harmony 补丁（拦截 Slot.Plug/Unplug）
- `Client/Core/EventBus/Events/WeaponSyncEvents.cs` - 事件定义
- `Client/Services/WeaponSyncHelper.cs` - 序列化/反序列化工具
- `Client/Services/WeaponSyncClientServiceImpl.cs` - 客户端服务实现
- `Client/Core/Players/RemotePlayer.cs` - 扩展武器数据管理
- `Client/Core/Players/LocalPlayer.cs` - 扩展武器上传功能
- `Client/ModBehaviour.cs` - 注册客户端服务

### Server (服务器)
- `Server/Services/WeaponSyncServerServiceImpl.cs` - 服务器服务实现
- `Server/Services/RoomServiceImpl.cs` - 扩展批量武器数据推送
- `Server/Program.cs` - 注册服务器服务

## 🎮 使用场景

### 场景1：玩家装备武器
1. 玩家在游戏中拾取并装备 AK-74 + 4倍镜 + 前握把
2. `Slot.Plug()` 被调用
3. Harmony 补丁拦截，创建事件
4. 序列化完整武器数据（包括配件）
5. 发送到服务器
6. 服务器广播给房间内其他玩家
7. 其他玩家看到完整的武器（含配件）

### 场景2：玩家加入房间
1. 玩家B加入房间
2. 服务器收集房间内所有玩家的武器数据
3. 批量发送给玩家B
4. 玩家B缓存所有武器数据
5. 当玩家B进入相同场景，其他玩家角色创建时
6. 自动应用缓存的武器数据

### 场景3：玩家卸下武器
1. 玩家卸下主武器
2. `Slot.Unplug()` 被 Prefix 拦截（可访问卸下前的内容）
3. 发送 `UnequipWeaponAsync` 请求
4. 服务器更新数据并广播
5. 其他玩家端移除该武器显示

## 🔄 与装备系统的对比

| 特性 | 装备系统 | 武器系统 |
|------|----------|----------|
| **数据量** | 仅 TypeID | 完整物品树 |
| **拦截方法** | `CharacterEquipmentController.ChangeXxxModel()` | `Slot.Plug/Unplug()` |
| **槽位数量** | 5个（护甲、头盔、面罩、背包、耳机） | 3个（主武器、副武器、近战） |
| **序列化** | 不需要 | 完整序列化（ItemTreeData） |
| **增量优化** | 无 | 默认物品不传输数据 |
| **子物品** | 无 | 有（配件、弹药） |

## ⚡ 性能优化

1. **增量同步**
   - 默认武器不传输完整数据
   - 仅传输 TypeID，接收端自行创建

2. **对象池**
   - `SerializationPool` 复用序列化对象
   - 减少 GC 压力

3. **异步处理**
   - 序列化和 RPC 调用都是异步的
   - 不阻塞主线程

4. **延迟应用**
   - 角色创建后延迟2帧应用武器
   - 确保模型初始化完成

## 🐛 调试日志

系统在 DEBUG 模式下会输出详细日志：

```
[武器补丁] 主武器已装备: AK74_1(Clone)
[武器补丁] 事件已发布: 主武器 - 装备 - AK74_1(Clone)
[武器补丁] ✅ 武器装备已同步到服务器: 主武器
[WeaponSyncHelper] 完整同步 - 自定义武器，数据长度=2048
[WeaponSyncService] 玩家 PlayerName 装备武器: PrimaryWeapon = AK74 (数据=2048字节)
[WeaponSyncService] 武器更新已广播给 2 个玩家 (房间: Room_001)
[WeaponSyncClientService] 收到武器更新: 玩家=76561198XXXXXXX, 槽位=PrimaryWeapon, 动作=装备, 武器=AK74
[RemotePlayer] 🔫 开始应用缓存的武器: 1 件
[RemotePlayer] ✅ 武器已应用: PrimaryWeapon = AK74
```

## 🎯 测试建议

1. **基础测试**
   - 装备/卸下主武器
   - 装备/卸下副武器
   - 装备/卸下近战武器

2. **配件测试**
   - 装备带配件的武器（瞄具、握把等）
   - 验证远程玩家能看到配件

3. **多人测试**
   - 2个玩家在同一房间
   - A装备武器，B应该看到
   - A卸下武器，B应该看不到

4. **场景切换测试**
   - A和B在不同场景
   - A装备武器
   - B进入A所在场景
   - B应该看到A的武器

## 📝 已知限制

1. **不同步武器状态**
   - 当前只同步武器装备/卸下
   - 不同步射击、换弹等动作
   - 不同步弹药数量变化（除非重新装备）

2. **性能考虑**
   - 带大量配件的武器数据可能较大
   - 建议限制单个武器数据不超过10KB

3. **兼容性**
   - 依赖游戏本体的 `ItemTreeData` API
   - 游戏更新可能影响序列化兼容性

## 🚀 未来扩展

1. **弹药同步**
   - 实时同步当前弹药数
   - 同步换弹动作

2. **射击同步**
   - 同步开火动作
   - 同步弹道效果

3. **武器改装同步**
   - 实时同步配件更换
   - 不需要卸下重装

## ✅ 实现完成

- ✅ Harmony 补丁系统
- ✅ 事件系统
- ✅ 数据结构定义
- ✅ 完整序列化/反序列化
- ✅ RPC 服务接口
- ✅ 服务器端实现
- ✅ 客户端实现
- ✅ 远程玩家武器管理
- ✅ 本地玩家武器上传
- ✅ 加入房间批量推送
- ✅ 增量同步优化
- ✅ 代理类自动生成
- ✅ 编译通过

---

**作者**: AI Assistant  
**日期**: 2025-11-04  
**版本**: 1.0.0

