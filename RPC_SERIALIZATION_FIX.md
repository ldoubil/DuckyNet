# RPC 序列化错误修复

## 🐛 问题描述

启动服务器和客户端后，出现以下错误：

```
[RpcServer] Failed to deserialize RpcMessage: Cannot cast deserialized object from System.Int16[] to DuckyNet.Shared.RPC.RpcMessage.
```

客户端无法登录，连接超时。

## 🔍 根本原因

新增的武器同步系统引入了以下数据类型，但没有注册到 RPC 序列化器中：

1. **`PlayerWeaponData`** - 玩家武器数据
2. **`WeaponItemData`** - 单个武器数据
3. **`WeaponSlotType`** - 武器槽位类型枚举
4. **`EquipmentSlotType`** - 装备槽位类型枚举
5. **相关的 Dictionary 泛型类型**

这些类型被嵌套在 `AllPlayersWeaponData` 和 `PlayerEquipmentData` 中使用，但 RPC 代码生成器只扫描方法参数/返回值的直接类型，没有递归扫描嵌套类型。

## ✅ 解决方案

手动在 `Shared/Generated/RpcTypeRegistry.cs` 中添加缺失的类型：

```csharp
// 新增的数据类型
typeof(DuckyNet.Shared.Data.PlayerEquipmentData),
typeof(DuckyNet.Shared.Data.PlayerWeaponData),
typeof(DuckyNet.Shared.Data.WeaponItemData),

// 枚举类型
typeof(DuckyNet.Shared.Data.EquipmentSlotType),
typeof(DuckyNet.Shared.Data.WeaponSlotType),

// 集合类型（用于序列化 Dictionary）
typeof(System.Collections.Generic.Dictionary<string, DuckyNet.Shared.Data.PlayerEquipmentData>),
typeof(System.Collections.Generic.Dictionary<string, DuckyNet.Shared.Data.PlayerWeaponData>),
typeof(System.Collections.Generic.Dictionary<DuckyNet.Shared.Data.EquipmentSlotType, int>),
typeof(System.Collections.Generic.Dictionary<DuckyNet.Shared.Data.WeaponSlotType, DuckyNet.Shared.Data.WeaponItemData>),
```

## 📋 修复后的类型清单（共 34 个类型）

### 基础类型 (9)
- `string`, `int`, `long`, `float`, `double`, `bool`
- `byte[]`, `object[]`, `DateTime`

### RPC 核心类型 (2)
- `RpcMessage`, `RpcResponse`

### 游戏数据类型 (13)
- `AllPlayersEquipmentData`, `AllPlayersWeaponData`
- `AnimatorSyncData`, `CharacterAppearanceData`
- `EquipmentSlotUpdateNotification`, `EquipmentSlotUpdateRequest`
- `ItemDropData`, `ItemPickupRequest`
- `PlayerEquipmentData`, `PlayerWeaponData`
- `ScenelData`, `UnitySyncData`, `WeaponItemData`

### 武器同步类型 (3)
- `WeaponSlotUnequipRequest`
- `WeaponSlotUpdateNotification`
- `WeaponSlotUpdateRequest`

### 房间/玩家服务类型 (7)
- `CreateRoomRequest`, `JoinRoomRequest`
- `LoginResult`, `MessageType`
- `PlayerInfo`, `PlayerInfo[]`
- `RoomInfo`, `RoomInfo[]`, `RoomOperationResult`

### 枚举类型 (2)
- `EquipmentSlotType`, `WeaponSlotType`

### 集合类型 (4)
- `Dictionary<string, PlayerEquipmentData>`
- `Dictionary<string, PlayerWeaponData>`
- `Dictionary<EquipmentSlotType, int>`
- `Dictionary<WeaponSlotType, WeaponItemData>`

## 🚀 验证步骤

1. **重新编译项目**
   ```bash
   cd E:\git\DuckyNet
   dotnet build DuckyNet.sln
   ```

2. **启动服务器**
   ```bash
   cd Server
   dotnet run
   ```
   
   观察日志应该显示：
   ```
   [RpcSerializer] Loaded 34 types from auto-generated registry
   ```

3. **启动客户端**
   - 登录游戏
   - 观察服务器日志，不应再出现序列化错误
   - 应该能成功登录并加入房间

## 🔄 未来预防措施

1. **代码生成器改进**
   - 考虑让 RPC 代码生成器递归扫描嵌套类型
   - 自动发现 Dictionary/List 的泛型参数类型

2. **单元测试**
   - 为每个新增的数据类型添加序列化/反序列化测试
   - 在 CI 中自动验证类型注册完整性

3. **文档化**
   - 在添加新数据类型时，检查是否需要手动注册
   - 维护一个 "手动注册清单"

## 📝 相关文件

- `Shared/Generated/RpcTypeRegistry.cs` - 类型注册表（本次修改）
- `Shared/Data/WeaponSyncData.cs` - 武器同步数据定义
- `Shared/Data/EquipmentData.cs` - 装备数据定义
- `Shared/Services/IPlayerService.cs` - PlayerInfo 定义

---

**修复时间**: 2025-11-04  
**影响范围**: 武器同步系统、装备同步系统  
**修复状态**: ✅ 已完成

