# 装备同步系统架构文档

## 📐 系统概述

装备同步系统实现了多人游戏中玩家装备的实时同步，包括5个装备槽位：
- 护甲 (Armor)
- 头盔 (Helmet)
- 面罩 (FaceMask)
- 背包 (Backpack)
- 耳机 (Headset)

## 🏗️ 架构设计

### 核心理念
- **数据与视图分离**：装备数据存储在变量中，角色模型只是视觉显示
- **事件驱动**：加入房间后靠事件实时同步
- **双端存储**：服务器持久化 + 客户端 RemotePlayer 运行时缓存

### 数据结构

```
服务器端：
Dictionary<string, PlayerInfo>
           └── PlayerInfo.EquipmentData: PlayerEquipmentData
                   └── Equipment: Dictionary<EquipmentSlotType, int>  // TypeID

客户端：
RemotePlayer._equipmentData: PlayerEquipmentData
    └── Equipment: Dictionary<EquipmentSlotType, int>  // TypeID
```

## 🔄 完整数据流

### 1️⃣ 本地玩家装备变更

```
[玩家装备护甲]
    ↓
EquipmentSlotPatch.Postfix_ChangeArmorModel()
    ├─→ 发布本地事件: ArmorSlotChangedEvent
    │   └→ EquipmentSyncDebugModule 接收（如果启用）
    │       └→ 同步到所有测试单位
    └─→ 发送RPC: UpdateEquipmentSlotAsync()
        └→ 服务器 EquipmentServerServiceImpl
            ├─→ 更新 PlayerInfo.EquipmentData
            └─→ 广播给房间内其他玩家
                └→ 客户端 EquipmentClientServiceImpl.OnEquipmentSlotUpdated()
                    ├─→ 更新 RemotePlayer._equipmentData
                    └─→ 如果角色已创建 → 立即应用装备
                        如果角色未创建 → 等待创建时应用
```

### 2️⃣ 加入房间时批量同步

```
[玩家加入房间]
    ↓
RoomServiceImpl.JoinRoomAsync()
    ├─→ 发送现有玩家列表给新玩家
    └─→ equipmentService.SendAllEquipmentDataToPlayer()
        └→ 客户端 EquipmentClientServiceImpl.OnAllPlayersEquipmentReceived()
            └→ 为每个远程玩家
                ├─→ remotePlayer.SetEquipmentData()
                └─→ 如果角色已创建 → 立即应用所有装备
                    如果角色未创建 → 等待创建时应用
```

### 3️⃣ 远程玩家角色创建

```
[远程玩家角色创建]
    ↓
RemotePlayer.CreateCharacter()
    ├─→ 创建角色模型
    ├─→ 应用外观数据
    └─→ ApplyCachedEquipmentDelayed()  // 延迟2帧
        └→ ApplyCachedEquipment()
            └→ 遍历 _equipmentData
                └→ EquipmentTools.CreateAndEquip()
                    └→ 视觉显示装备
```

## 📦 关键组件

### Shared 层（共享数据结构）

#### `EquipmentSlotType` 枚举
```csharp
public enum EquipmentSlotType : byte
{
    Armor = 0,
    Helmet = 1,
    FaceMask = 2,
    Backpack = 3,
    Headset = 4
}
```

#### `PlayerEquipmentData` 类
- `Equipment: Dictionary<EquipmentSlotType, int>` - 装备数据
- `SetEquipment()` - 设置槽位（null或0会删除键）
- `GetEquipment()` - 获取槽位
- `Clone()` - 克隆数据

#### RPC 消息
- `EquipmentSlotUpdateRequest` - 客户端→服务器
- `EquipmentSlotUpdateNotification` - 服务器→客户端（单个槽位）
- `AllPlayersEquipmentData` - 服务器→客户端（批量）

#### RPC 服务接口
- `IEquipmentService` - 客户端调用服务器
- `IEquipmentClientService` - 服务器调用客户端

### Server 层（服务器实现）

#### `EquipmentServerServiceImpl`
- `UpdateEquipmentSlotAsync()` - 处理客户端装备更新请求
- `BroadcastEquipmentUpdate()` - 广播给房间内其他玩家
- `SendAllEquipmentDataToPlayer()` - 玩家加入房间时发送批量数据

#### `PlayerInfo.EquipmentData`
- 服务器端持久化存储
- 每个玩家的装备数据

### Client 层（客户端实现）

#### 1. 补丁系统
**`EquipmentSlotPatch.cs`**
- 监控本地玩家的5个装备槽位变更
- `IsMainCharacter()` - 过滤非本地玩家
- `PublishEquipmentEvent()` - 发布本地事件 + 发送RPC
- `SendEquipmentUpdateToServerAsync()` - 异步发送到服务器

#### 2. 事件系统
**`EquipmentEvents.cs`**
- 装备变更事件（5种槽位类型）
- 统一使用 `Shared.Data.EquipmentSlotType`

#### 3. 客户端服务
**`EquipmentClientServiceImpl.cs`**
- `OnEquipmentSlotUpdated()` - 接收单个槽位更新
- `OnAllPlayersEquipmentReceived()` - 接收批量装备数据
- `ApplyEquipmentToCharacter()` - 应用装备到角色模型

#### 4. 远程玩家
**`RemotePlayer._equipmentData`**
- 缓存远程玩家的装备数据
- `SetEquipmentData()` - 批量设置
- `UpdateEquipmentSlot()` - 更新单个槽位
- `ApplyCachedEquipment()` - 应用到角色模型

#### 5. 工具类
**`EquipmentTools.cs`**
- `CreateAndEquip()` - 创建物品并装备
- `CopyAllEquipment()` - 复制所有装备
- `UnequipSlot()` - 卸下装备
- `ClearAllEquipment()` - 清空装备

#### 6. 调试模块
**`EquipmentSyncDebugModule.cs`**
- 创建测试单位
- 实时同步本地玩家装备
- 可视化调试界面

## 🔑 关键时机

### 加入房间
```
Player A 加入房间
  ↓
RoomService.JoinRoomAsync()
  ├─→ 发送房间内现有玩家列表
  └─→ equipmentService.SendAllPlayersEquipmentData()
      └→ Player A 接收所有玩家的装备数据
          └→ 缓存到各个 RemotePlayer._equipmentData
```

### 装备变更
```
Player A 装备护甲
  ↓
Patch 捕获 → RPC 发送 → Server 更新 → 广播给 Player B, C, D
  ↓
Player B/C/D 接收
  ├─→ 更新 RemotePlayer._equipmentData
  └─→ 如果角色已创建 → 立即应用
```

### 角色创建
```
RemotePlayer 创建角色
  ↓
CreateCharacter()
  ├─→ 创建模型
  ├─→ 应用外观
  └─→ ApplyCachedEquipmentDelayed()  // 延迟2帧
      └→ 读取 _equipmentData 并应用
```

## 📝 使用示例

### 服务器端
```csharp
// 服务器自动处理，无需手动调用
// 玩家加入房间时自动发送装备数据
// 装备变更时自动广播
```

### 客户端 - 装备变更自动同步
```csharp
// 玩家装备/卸下装备时，补丁自动捕获并同步
// 无需手动调用任何代码
```

### 客户端 - 手动操作装备
```csharp
using DuckyNet.Client.Core.Utils;

// 为角色装备物品
bool success = EquipmentTools.EquipToCharacter(
    itemTypeId: 1001,
    characterItem: character.CharacterItem,
    slotHash: CharacterEquipmentController.armorHash
);

// 卸下装备
Item? removed = EquipmentTools.UnequipSlot(
    character.CharacterItem,
    CharacterEquipmentController.armorHash
);
```

## 🎯 特性

### ✅ 已实现
- [x] 本地玩家装备变更自动捕获
- [x] 装备数据发送到服务器
- [x] 服务器持久化存储
- [x] 广播给房间内其他玩家
- [x] 客户端接收并更新 RemotePlayer 数据
- [x] 角色创建时自动应用装备
- [x] 实时装备更新（装备/卸下）
- [x] 加入房间时批量同步
- [x] 调试模块用于测试
- [x] 完整的日志系统

### 🔒 安全性
- [x] 只同步本地玩家的装备
- [x] 过滤非本地玩家触发的事件
- [x] 多层空值检查
- [x] 完整的异常处理

### ⚡ 性能优化
- [x] 仅存储 TypeID（不存储完整物品数据）
- [x] 使用 Hash 值访问槽位
- [x] 早期过滤非目标事件
- [x] 异步RPC调用不阻塞游戏

## 🧪 测试流程

1. **启动服务器**
   ```
   cd E:\git\DuckyNet\Server
   dotnet run
   ```

2. **启动游戏**
   - 运行游戏客户端
   - 登录并创建/加入房间

3. **测试本地同步**
   - 打开调试模块 "装备同步测试"
   - 点击 "创建测试单位"
   - 装备/卸下装备，观察测试单位是否同步

4. **测试网络同步**
   - 启动第二个客户端
   - 两个客户端加入同一房间
   - 客户端A装备护甲
   - 观察客户端B是否看到客户端A的装备

## 📋 调试日志示例

### 本地玩家装备护甲
```
[装备补丁] 护甲已装备: Armor_Kevlar
[装备补丁] 事件已发布: Armor - 装备 - Armor_Kevlar
[装备补丁] ✅ 装备更新已同步到服务器: Armor
[EquipmentService] 玩家 PlayerName 装备装备: Armor = 1001
[EquipmentService] 装备更新已广播给 1 个玩家 (房间: room123)
```

### 远程客户端接收
```
[EquipmentClientService] 收到装备更新: 玩家=steamid123, 槽位=Armor, 动作=装备, TypeID=1001
[RemotePlayer] 装备更新: PlayerName 装备 Armor (TypeID=1001)
[EquipmentClientService] ✅ 已应用装备: Armor = TypeID 1001
```

### 加入房间时
```
[EquipmentService] 已向玩家 steamid456 发送房间装备数据: 2 个玩家
[EquipmentClientService] 收到批量装备数据: 2 个玩家
[RemotePlayer] 装备数据已设置: PlayerName, 3 件装备
[EquipmentClientService] ✅ 批量装备数据处理完成
```

### 角色创建时应用装备
```
[RemotePlayer] ⏳ 等待角色初始化完成（装备系统）...
[RemotePlayer] 🎽 开始应用缓存的装备: 3 件
[RemotePlayer] ✅ 已应用装备: Armor = TypeID 1001
[RemotePlayer] ✅ 已应用装备: Helmet = TypeID 2001
[RemotePlayer] ✅ 已应用装备: Backpack = TypeID 4001
[RemotePlayer] 🎽 装备应用完成: 3/3
```

## 🚀 下一步

运行 RPC 代码生成器并编译项目：

```powershell
# 1. 生成 RPC 代理类（已完成）
dotnet run --project Tools\RpcCodeGen\RpcCodeGen.csproj

# 2. 编译项目
dotnet build

# 3. 启动服务器
cd Server
dotnet run

# 4. 启动游戏测试
```

## 🎮 测试检查清单

- [ ] 本地装备变更是否触发事件
- [ ] 装备数据是否发送到服务器
- [ ] 服务器是否正确存储
- [ ] 是否广播给其他玩家
- [ ] 其他玩家是否接收到更新
- [ ] RemotePlayer 数据是否正确更新
- [ ] 角色创建时是否应用装备
- [ ] 实时装备变更是否立即应用
- [ ] 卸下装备是否正确处理
- [ ] 加入房间时是否收到所有玩家装备

---

**创建日期**: 2025-11-04  
**版本**: 1.0.0  
**状态**: ✅ 完成实现，待测试

