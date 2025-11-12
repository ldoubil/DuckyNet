# NPC 同步优化 - 代码示例

## 场景演示

假设有两个玩家：
- **玩家 A**：已在场景中，有 3 个本地 NPC
- **玩家 B**：刚进入场景，需要看到玩家 A 的 NPC

---

## 流程 1：主动推送（玩家 A 创建新 NPC）

### 1️⃣ 玩家 A 的本地 NPC 生成

```csharp
// 客户端 A - NpcManager.cs
private void OnNpcSpawned(CharacterSpawnedEvent evt)
{
    var npcInfo = new NpcInfo { ... };
    _localNpcs[evt.CharacterId] = npcInfo;
    
    // 发送到服务器
    SendNpcSpawnToServer(npcInfo);
}
```

**日志输出**：
```
[NpcManager] 本地 NPC 已注册: Zombie(Clone) (ID: abc-123)
```

---

### 2️⃣ 服务端收到并主动推送

```csharp
// 服务端 - NpcSyncServiceImpl.cs
public async Task NotifyNpcSpawned(IClientContext client, NpcSpawnData spawnData)
{
    // 1. 记录到玩家的 NPC 列表
    _playerNpcManager.AddNpc(player.SteamId, spawnData);
    
    // 2. 🚀 主动推送给范围内的其他玩家
    var scenePlayers = ServerContext.Players.GetScenePlayers(player, excludeSelf: true);
    foreach (var targetPlayer in scenePlayers)
    {
        var change = _visibilityTracker.UpdatePlayerVisibility(...);
        
        // 如果新 NPC 在该玩家范围内，推送
        if (change.EnteredRange.Contains(spawnData.NpcId))
        {
            ServerContext.Broadcast.CallClientTyped<INpcSyncClientService>(targetPlayer,
                service => service.OnNpcSpawned(spawnData));
            Console.WriteLine($"🚀 主动推送 NPC {spawnData.NpcId} 给 {targetPlayer.SteamName}");
        }
    }
}
```

**日志输出**（服务端）：
```
[NpcSyncService] 📥 收到 NPC 生成: Zombie(Clone) (ID: abc-123, 来自: PlayerA)
[NpcSyncService] 🚀 主动推送 NPC abc-123 给 PlayerB
[NpcSyncService] ✅ NPC 已记录并推送给 1 个玩家
```

---

### 3️⃣ 玩家 B 收到推送并创建

```csharp
// 客户端 B - NpcSyncClientServiceImpl.cs
public void OnNpcSpawned(NpcSpawnData spawnData)
{
    Debug.Log($"📦 收到远程 NPC 生成: {spawnData.NpcType} (ID: {spawnData.NpcId})");
    
    // 检查场景是否匹配
    if (isSameScene)
    {
        // 从对象池创建影子 NPC
        GameContext.Instance.NpcManager?.AddRemoteNpc(spawnData.NpcId, spawnData);
    }
}
```

**日志输出**（客户端 B）：
```
[NpcSyncClient] 📦 收到远程 NPC 生成: Zombie(Clone) (ID: abc-123)
[NpcManager] ✅ 远程 NPC 已添加: Zombie(Clone) (ID: abc-123)
```

**结果**：玩家 B 立即看到玩家 A 的新 NPC，无延迟！

---

## 流程 2：按需请求（玩家 B 收到位置更新但缺失 NPC）

### 场景：服务器主动推送失败，但位置更新正常到达

### 1️⃣ 玩家 B 收到位置更新

```csharp
// 客户端 B - NpcSyncClientServiceImpl.cs
public void OnNpcBatchTransform(NpcBatchTransformData batchData)
{
    int missingCount = 0;
    
    for (int i = 0; i < batchData.Count; i++)
    {
        string npcId = batchData.NpcIds[i];
        var npc = npcManager.GetNpc(npcId);
        
        if (npc != null)
        {
            // ✅ NPC 存在，更新位置
            npcManager.UpdateRemoteNpcTransform(npcId, position, rotationY);
        }
        else
        {
            // ⚠️ NPC 不存在，请求创建
            if (npcManager.CheckAndRequestMissingNpc(npcId))
            {
                missingCount++;
                Debug.Log($"🔍 发现缺失 NPC，已请求: {npcId}");
            }
        }
    }
}
```

**日志输出**（客户端 B）：
```
[NpcSyncClient] 🔍 发现缺失 NPC，已请求: abc-123
[NpcSyncClient] 位置更新完成: 2 个更新, 1 个请求创建
```

---

### 2️⃣ 客户端 B 请求缺失 NPC

```csharp
// 客户端 B - NpcManager.cs
public bool CheckAndRequestMissingNpc(string npcId)
{
    // 1. 已存在？跳过
    if (_localNpcs.ContainsKey(npcId) || _remoteNpcs.ContainsKey(npcId))
        return false;

    // 2. 正在请求或已失败？跳过
    if (_pendingRequests.Contains(npcId) || _failedRequests.Contains(npcId))
        return false;

    // 3. 节流检查
    if (Time.time - _lastRequestTime < RequestThrottle)
        return false; // 太频繁，等待下次

    // 4. 发起请求
    RequestSingleNpcAsync(npcId);
    return true;
}

private async void RequestSingleNpcAsync(string npcId)
{
    _pendingRequests.Add(npcId);
    Debug.Log($"🔍 请求缺失 NPC: {npcId}");
    
    var npcData = await npcService.RequestSingleNpc(npcId);
    
    if (npcData != null)
    {
        Debug.Log($"✅ 收到 NPC 数据: {npcData.NpcType} (ID: {npcId})");
        AddRemoteNpc(npcId, npcData);
    }
    else
    {
        Debug.LogWarning($"⚠️ NPC 不存在或超出范围: {npcId}");
        _failedRequests.Add(npcId);
    }
    
    _pendingRequests.Remove(npcId);
}
```

**日志输出**（客户端 B）：
```
[NpcManager] 🔍 请求缺失 NPC: abc-123
[NpcManager] ✅ 收到 NPC 数据: Zombie(Clone) (ID: abc-123)
[NpcManager] ✅ 远程 NPC 已添加: Zombie(Clone) (ID: abc-123)
```

---

### 3️⃣ 服务端处理单个请求

```csharp
// 服务端 - NpcSyncServiceImpl.cs
public Task<NpcSpawnData?> RequestSingleNpc(IClientContext client, string npcId)
{
    var player = ServerContext.Players.GetPlayer(client.ClientId);
    Console.WriteLine($"📥 玩家请求单个 NPC: {player.SteamName} → {npcId}");
    
    // 从所有玩家的 NPC 中查找
    var npc = _playerNpcManager.GetNpcById(npcId);
    if (npc == null)
    {
        Console.WriteLine($"⚠️ NPC 不存在: {npcId}");
        return Task.FromResult<NpcSpawnData?>(null);
    }
    
    // 检查可见性
    var distance = CalculateDistance(player, npc);
    if (distance > _visibilityTracker.VisibilityRange)
    {
        Console.WriteLine($"⚠️ NPC 超出范围: {npcId} (距离: {distance:F1}m)");
        return Task.FromResult<NpcSpawnData?>(null);
    }
    
    Console.WriteLine($"✅ 返回单个 NPC: {npcId} (距离: {distance:F1}m)");
    return Task.FromResult<NpcSpawnData?>(npc);
}
```

**日志输出**（服务端）：
```
[NpcSyncService] 📥 玩家请求单个 NPC: PlayerB → abc-123
[NpcSyncService] ✅ 返回单个 NPC: abc-123 (距离: 45.2m)
```

**结果**：玩家 B 成功获取并创建缺失的 NPC！

---

## 流程 3：去重和节流示例

### 场景：短时间内收到多次同一个 NPC 的位置更新

```csharp
// 第 1 次位置更新（时间 0ms）
OnNpcBatchTransform([abc-123, def-456])
    → abc-123 缺失 → 请求（✅ 成功）
    → def-456 缺失 → 请求（✅ 成功）

// 第 2 次位置更新（时间 50ms）
OnNpcBatchTransform([abc-123, def-456])
    → abc-123 缺失 → 请求（❌ 已在 _pendingRequests，跳过）
    → def-456 缺失 → 请求（❌ 已在 _pendingRequests，跳过）

// 第 3 次位置更新（时间 100ms，节流期内）
OnNpcBatchTransform([ghi-789])
    → ghi-789 缺失 → 请求（❌ 节流中，跳过）

// 第 4 次位置更新（时间 250ms，节流期外）
OnNpcBatchTransform([ghi-789])
    → ghi-789 缺失 → 请求（✅ 成功）
```

**日志输出**：
```
[NpcSyncClient] 🔍 发现缺失 NPC，已请求: abc-123
[NpcSyncClient] 🔍 发现缺失 NPC，已请求: def-456
[NpcManager] 🔍 请求缺失 NPC: abc-123
[NpcManager] 🔍 请求缺失 NPC: def-456
// ... 50ms 后，重复请求被去重 ...
// ... 250ms 后 ...
[NpcSyncClient] 🔍 发现缺失 NPC，已请求: ghi-789
[NpcManager] 🔍 请求缺失 NPC: ghi-789
```

---

## 关键参数配置

```csharp
// 客户端 - NpcManager.cs
private const float RequestThrottle = 0.2f; // 200ms 节流

// 服务端 - NpcVisibilityTracker.cs
public float VisibilityRange = 100f; // 可见性范围 100 米
```

**调整建议**：
- `RequestThrottle`：根据网络延迟调整（低延迟可减小到 100ms）
- `VisibilityRange`：根据游戏地图大小调整（大地图可增加到 150m）

---

## 完整流程图

```
客户端 A 创建 NPC
    ↓
[NotifyNpcSpawned] 发送到服务端
    ↓
服务端记录 NPC
    ↓
服务端检测范围内玩家（玩家 B）
    ↓
[OnNpcSpawned] 主动推送给玩家 B
    ↓
客户端 B 创建影子 NPC
    ↓
（如果推送失败或延迟）
    ↓
客户端 A 发送位置更新
    ↓
[OnNpcBatchTransform] 玩家 B 收到
    ↓
玩家 B 检测本地没有该 NPC
    ↓
[CheckAndRequestMissingNpc] 检查去重和节流
    ↓
[RequestSingleNpc] 请求服务端
    ↓
服务端返回 NPC 数据（带可见性检查）
    ↓
客户端 B 创建影子 NPC
    ↓
完成！
```

---

## 测试用例

### ✅ 测试 1：主动推送
**步骤**：
1. 玩家 A 和玩家 B 在同一场景
2. 玩家 A 触发 NPC 生成
3. 检查玩家 B 是否立即看到 NPC

**预期结果**：
- 服务端日志：`🚀 主动推送 NPC abc-123 给 PlayerB`
- 客户端 B 日志：`📦 收到远程 NPC 生成`

---

### ✅ 测试 2：按需请求
**步骤**：
1. 玩家 A 已有 NPC（玩家 B 未收到主动推送）
2. 玩家 B 收到位置更新
3. 检查玩家 B 是否自动请求并创建 NPC

**预期结果**：
- 客户端 B 日志：`🔍 发现缺失 NPC，已请求: abc-123`
- 客户端 B 日志：`✅ 收到 NPC 数据: Zombie(Clone)`

---

### ✅ 测试 3：去重
**步骤**：
1. 快速触发多次位置更新（同一个缺失 NPC）
2. 检查是否只请求一次

**预期结果**：
- 只有一次 `🔍 请求缺失 NPC` 日志
- 后续请求被去重跳过

---

### ✅ 测试 4：节流
**步骤**：
1. 在 200ms 内发起多个不同 NPC 的请求
2. 检查是否受到节流限制

**预期结果**：
- 第一个请求成功
- 后续请求被节流跳过（直到 200ms 后）

---

生成时间: 2025-11-12

