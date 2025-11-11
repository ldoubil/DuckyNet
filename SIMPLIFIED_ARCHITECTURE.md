# 🎯 简化架构：玩家 → NPC 列表映射

## ✅ 重构完成

### 核心设计原则
按照用户要求重构为更简洁的架构：

1. **客户端职责：**
   - 发送创建单位（NPC Spawn）
   - 发送单位销毁（NPC Destroy）
   - 发送位置更新（Position Update）

2. **服务器职责：**
   - 维护 `玩家 → NPC 列表` 的 Map
   - **位置更新时**触发可见性检查
   - 过滤并发送范围内的 NPC 给远程玩家
   - 远程玩家动态创建/更新/销毁

---

## 📁 新增文件

### `Server/Managers/PlayerNpcManager.cs` ✨
**作用：** 维护每个玩家拥有的 NPC 列表

**核心数据结构：**
```csharp
// 玩家 ID (SteamId) -> NPC 列表
Dictionary<string, List<NpcSpawnData>> _playerNpcs

// NPC ID -> 拥有者玩家 ID (快速查找)
Dictionary<string, string> _npcOwners
```

**主要方法：**
- `AddNpc(playerId, npcData)` - 添加玩家的 NPC
- `RemoveNpc(npcId)` - 移除 NPC
- `UpdateNpcPosition(npcId, x, y, z, rotY)` - 更新位置
- `GetPlayerNpcs(playerId)` - 获取玩家的所有 NPC
- `GetSceneNpcs(sceneName, subSceneName)` - 获取场景所有 NPC
- `ClearPlayerNpcs(playerId)` - 清理玩家的所有 NPC（断开时）

---

## 🔧 修改的文件

### `Server/Services/NpcSyncServiceImpl.cs`

#### 1. **NotifyNpcSpawned** - 只记录，不广播
```csharp
public async Task NotifyNpcSpawned(IClientContext client, NpcSpawnData spawnData)
{
    // 🔥 只记录到玩家的 NPC 列表，不立即广播
    // 等其他玩家位置更新时，会自动检测并发送
    _playerNpcManager.AddNpc(player.SteamId, spawnData);
    
    Console.WriteLine("✅ NPC 已记录到玩家列表（等待靠近时动态同步）");
}
```

**变化：**
- ❌ 旧：立即检查所有玩家可见性并广播
- ✅ 新：只记录到列表，不广播

---

#### 2. **NotifyNpcBatchTransform** - 核心同步逻辑 ⭐
```csharp
public async Task NotifyNpcBatchTransform(IClientContext client, NpcBatchTransformData batchData)
{
    // 1. 更新服务器记录的 NPC 位置
    for (int i = 0; i < batchData.Count; i++)
    {
        _playerNpcManager.UpdateNpcPosition(...);
    }

    // 2. 获取同场景的其他玩家
    var scenePlayers = ServerContext.Players.GetScenePlayers(player, excludeSelf: true);

    // 3. 获取场景所有玩家的 NPC
    var allNpcs = _playerNpcManager.GetSceneNpcs(sceneName, subSceneName);

    // 4. 🔥 对每个远程玩家动态检测可见性变化
    foreach (var targetPlayer in scenePlayers)
    {
        var change = _visibilityTracker.UpdatePlayerVisibility(...);

        // 🆕 处理进入范围的 NPC（发送创建）
        foreach (var enteredNpcId in change.EnteredRange)
        {
            SendNpcSpawned(targetPlayer, enteredNpc);
        }

        // 🗑️ 处理离开范围的 NPC（发送销毁）
        foreach (var leftNpcId in change.LeftRange)
        {
            SendNpcDestroyed(targetPlayer, leftNpcId);
        }

        // 🔄 过滤可见的 NPC（发送位置更新）
        SendNpcBatchTransform(targetPlayer, filteredBatch);
    }
}
```

**变化：**
- ✅ 新增：更新服务器记录的 NPC 位置
- ✅ 保留：动态可见性检测
- ✅ 保留：进入/离开范围的自动创建/销毁

---

#### 3. **NotifyNpcDestroyed** - 简化
```csharp
public async Task NotifyNpcDestroyed(IClientContext client, NpcDestroyData destroyData)
{
    // 从玩家的 NPC 列表中移除
    _playerNpcManager.RemoveNpc(destroyData.NpcId);

    // 广播给同场景的其他玩家
    ServerContext.Broadcast.BroadcastToSceneTyped<INpcSyncClientService>(player, 
        service => service.OnNpcDestroyed(destroyData), 
        excludeSelf: true);
}
```

**变化：**
- ❌ 移除：查询 NPC 所属场景
- ✅ 简化：直接从列表移除

---

#### 4. **RequestSceneNpcs** - 改用 PlayerNpcManager
```csharp
public async Task<NpcSpawnData[]> RequestSceneNpcs(IClientContext client, string sceneName, string subSceneName)
{
    // 获取场景所有玩家的 NPC
    var allNpcs = _playerNpcManager.GetSceneNpcs(sceneName, subSceneName);

    // 🔥 初始化该玩家的可见性
    var change = _visibilityTracker.UpdatePlayerVisibility(...);

    // 只返回可见范围内的 NPC
    return allNpcs.Where(n => change.CurrentVisible.Contains(n.NpcId)).ToArray();
}
```

**变化：**
- ✅ 改用：`_playerNpcManager.GetSceneNpcs()` 替代 `_npcManager.GetSceneNpcs()`

---

### `Server/Core/ServiceCollectionExtensions.cs`

```csharp
// 🔥 NPC 管理（改用 PlayerNpcManager）
services.AddSingleton<PlayerNpcManager>();
```

**变化：**
- ❌ 移除：`NpcSceneManager`
- ✅ 新增：`PlayerNpcManager`

---

## 📊 完整数据流

### 场景 1：玩家 A 创建 NPC

```
客户端 A:
  游戏生成 NPC_1 at (10, 0, 0)
    ↓
  SendNpcSpawn(NPC_1) → 服务器
  
服务器:
  NotifyNpcSpawned(玩家A, NPC_1)
    ↓
  _playerNpcs[SteamId_A].Add(NPC_1) ✅
    ↓
  不立即广播（等待其他玩家靠近）
```

---

### 场景 2：玩家 B 加入并靠近

```
T+0s: 玩家 B 进入场景
客户端 B:
  延迟 1 秒后 RequestSceneNpcs()
    ↓
服务器:
  RequestSceneNpcs(玩家B)
    ↓
  GetSceneNpcs() → [NPC_1] (从玩家A的列表)
    ↓
  UpdatePlayerVisibility(玩家B, [NPC_1])
    ↓
  if (距离 < 100m) → 返回 [NPC_1]
  else → 返回 []
  
T+5s: 玩家 B 走近（<100m）
客户端 B:
  每 100ms 发送位置同步
    ↓
  不涉及 NPC（B 没有创建 NPC）
  
客户端 A:
  NPC_1 移动 → SendNpcBatchTransform([NPC_1])
    ↓
服务器:
  NotifyNpcBatchTransform(玩家A, [NPC_1])
    ↓
  UpdateNpcPosition(NPC_1) → 更新服务器记录 ✅
    ↓
  对玩家 B:
    UpdatePlayerVisibility(玩家B, [NPC_1])
      上次: CurrentVisible = []
      本次: CurrentVisible = [NPC_1]
      EnteredRange = [NPC_1] 🆕
    ↓
  发送 OnNpcSpawned(NPC_1) 给玩家 B ✅
  
客户端 B:
  收到 OnNpcSpawned(NPC_1)
    ↓
  _npcPool.Get(NPC_1) → 创建影子 NPC ✅
```

---

### 场景 3：玩家 B 走远

```
T+20s: 玩家 B 走远（>100m）

客户端 A:
  NPC_1 移动 → SendNpcBatchTransform([NPC_1])
    ↓
服务器:
  NotifyNpcBatchTransform(玩家A, [NPC_1])
    ↓
  对玩家 B:
    UpdatePlayerVisibility(玩家B, [NPC_1])
      上次: CurrentVisible = [NPC_1]
      本次: CurrentVisible = []
      LeftRange = [NPC_1] 🗑️
    ↓
  发送 OnNpcDestroyed(NPC_1, Reason=1) 给玩家 B ✅
  
客户端 B:
  收到 OnNpcDestroyed(NPC_1)
    ↓
  _npcPool.Recycle(NPC_1) → 回收到池 ♻️
```

---

## 🎯 优势

### 1. **简化逻辑**
- ❌ 旧：全局 NPC 列表，需要维护场景索引
- ✅ 新：玩家 → NPC 列表，自然关联

### 2. **性能优化**
- ❌ 旧：创建时立即检查所有玩家可见性
- ✅ 新：位置更新时才检查，减少不必要的计算

### 3. **清晰的职责**
- **客户端：** 只负责发送创建/销毁/位置
- **服务器：** 负责记录、过滤、动态同步

### 4. **自动清理**
- 玩家断开时，自动清理其所有 NPC
- 不需要手动维护场景索引

---

## 🔧 编译步骤

### 1. 确认服务器已关闭
```
Ctrl+C 停止服务器
```

### 2. 编译服务器
```bash
cd E:\git\DuckyNet
dotnet build Server/DuckyNetServer.csproj --no-incremental
```

### 3. 启动服务器
```bash
cd Server/bin/Debug/net8.0
DuckyNet.Server.exe
```

### 4. 启动游戏并测试

---

## 📊 预期日志

### 服务器端

**玩家 A 创建 NPC：**
```
[NpcSyncService] 📥 收到 NPC 生成: Character(Clone) (ID: xxx, 来自: 玩家A)
[PlayerNpcManager] 玩家 SteamId_A 创建 NPC: xxx
[NpcSyncService] ✅ NPC 已记录到玩家列表（等待靠近时动态同步）
```

**玩家 B 靠近时（NPC 位置更新触发）：**
```
[NpcVisibilityTracker] 玩家 玩家B 位置: (50.00, 0.00, 50.00)
  → NPC xxx 在范围内: 66.00m < 100.00m
[NpcSyncService] 🆕 NPC xxx 进入 玩家B 范围
```

**玩家 B 走远时：**
```
[NpcVisibilityTracker] 玩家 玩家B 位置: (150.00, 0.00, 150.00)
  → NPC xxx 超出范围: 205.00m > 100.00m
[NpcSyncService] 🗑️ NPC xxx 离开 玩家B 范围
```

### 客户端

**玩家 B 收到动态创建：**
```
[NpcSyncClient] 📦 收到远程 NPC 生成: Character(Clone) (ID: xxx)
[NpcManager] ✅ 远程 NPC 已添加: Character(Clone) (ID: xxx)
[ShadowNpcPool] ♻️ 复用 NPC (池剩余: 4)
```

**玩家 B 收到动态销毁：**
```
[NpcSyncClient] 🗑️ 收到远程 NPC 销毁: xxx (Reason: 1 - 超出范围)
[NpcManager] 远程 NPC 已移除: xxx
[ShadowNpcPool] ♻️ 回收 NPC (池剩余: 5)
```

---

## ✅ 完整实现确认

| 功能 | 状态 | 说明 |
|------|------|------|
| 玩家 → NPC 列表映射 | ✅ | PlayerNpcManager 实现 |
| 只记录不广播 | ✅ | NotifyNpcSpawned 简化 |
| 位置更新触发同步 | ✅ | NotifyNpcBatchTransform 核心逻辑 |
| 动态创建（进入范围） | ✅ | EnteredRange → OnNpcSpawned |
| 动态销毁（离开范围） | ✅ | LeftRange → OnNpcDestroyed |
| 玩家断开清理 | ✅ | ClearPlayerNpcs |
| 热区系统禁用 | ✅ | 简化调试 |
| 距离详细日志 | ✅ | 每个 NPC 显示距离 |
| 客户端重复检查 | ✅ | 跳过本地 NPC |

---

**架构重构完成！关闭服务器后立即编译测试！** 🚀

