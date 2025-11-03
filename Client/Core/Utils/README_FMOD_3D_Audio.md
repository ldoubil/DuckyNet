# FMOD 3D 音效系统说明

## 概述

DuckyNet 使用 **FMOD 3D 空间音效系统**，所有游戏音效都支持空间定位和距离衰减。

## 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                    FMOD 3D 音效系统                          │
└─────────────────────────────────────────────────────────────┘

                        游戏世界
    ┌────────────────────────────────────────────┐
    │                                            │
    │    🎧 音频监听器 (AudioListener)           │
    │    位置: 主角 + Vector3.up * 2             │
    │    旋转: 游戏相机旋转                       │
    │         ↑                                  │
    │         │ 2米                              │
    │         │                                  │
    │    🚶 主角 (CharacterMainControl.Main)     │
    │         │                                  │
    │         │                                  │
    │         │ 距离计算                          │
    │         │                                  │
    │         ↓                                  │
    │    🔫 枪械 GameObject                      │
    │    🔊 音源位置                             │
    │    (实时更新)                              │
    │                                            │
    └────────────────────────────────────────────┘
                        ↓
            ┌───────────────────────┐
            │    FMOD 音频引擎       │
            ├───────────────────────┤
            │ • 计算距离衰减         │
            │ • 计算立体声定位       │
            │ • 应用环境效果         │
            └───────────────────────┘
                        ↓
                ┌──────────────┐
                │  音箱/耳机    │
                │  🔊  🔊      │
                └──────────────┘
```

## 音效播放流程

### 1. 播放音效
```csharp
// 在枪械位置播放音效
AudioManager.Post("SFX/Combat/Gun/Shoot/ak47", gun.gameObject);
```

### 2. 创建 AudioObject
```csharp
// AudioManager 内部创建 AudioObject
var audioObject = gameObject.AddComponent<AudioObject>();
audioObject.eventName = eventName;
audioObject.Initialize();
```

### 3. 设置 3D 属性
```csharp
// AudioObject.Post() 设置音源位置
eventInstance.set3DAttributes(gameObject.transform.position.To3DAttributes());
```

### 4. 实时更新（FixedUpdate）
```csharp
// AudioObject.FixedUpdate() - 每个物理帧更新
void FixedUpdate()
{
    foreach (var eventInstance in _activeEvents)
    {
        // 更新音源位置（支持移动音源）
        eventInstance.set3DAttributes(transform.position.To3DAttributes());
    }
}
```

### 5. 更新监听器（每帧）
```csharp
// AudioManager.UpdateListener() - 每帧更新
void Update()
{
    if (CharacterMainControl.Main != null)
    {
        // 监听器位置 = 主角位置 + 向上 2 米
        Vector3 listenerPos = CharacterMainControl.Main.transform.position + Vector3.up * 2f;
        
        // 监听器旋转 = 相机旋转
        Quaternion listenerRot = GameManager.IngameCamera.transform.rotation;
        
        // 更新 FMOD 监听器
        FMODUnity.RuntimeManager.StudioSystem.setListenerAttributes(0, 
            listenerPos.To3DAttributes(), 
            listenerRot.ToFMODQuat());
    }
}
```

## 3D 音效参数

### 距离衰减曲线

```
音量 (Volume)
  │
1 │ ████                      
  │     ████                  
  │         ████              
  │             ████          
  │                 ████      
  │                     ████  
0 └─────────────────────────── 距离 (Distance)
  0    5    10   15   20   25  (米)
  
  近距离：音量100%
  中距离：音量逐渐衰减
  远距离：音量接近0%
```

### 立体声定位

```
        前方 (0°)
           🔊
           ↑
           │
左 ←───────┼───────→ 右
(90°)      │      (-90°)
      🎧 玩家
           │
           ↓
        后方 (180°)
        
左侧枪声 → 左声道音量 > 右声道音量
右侧枪声 → 右声道音量 > 左声道音量
前方枪声 → 左右声道平衡
后方枪声 → 左右声道平衡 + 混响
```

## 实际应用场景

### 场景 1：玩家自己开枪
```
玩家持枪开火
    ↓
音源 = 枪械 GameObject (玩家手中)
监听器 = 玩家位置 + 向上2米
    ↓
距离 ≈ 2米（很近）
    ↓
音量 = 100% （满音量）
方向 = 正前方
    ↓
结果：清晰洪亮的枪声
```

### 场景 2：远处 AI 开枪
```
AI 在50米外开枪
    ↓
音源 = AI 枪械位置
监听器 = 玩家位置 + 2米
    ↓
距离 = 50米
    ↓
音量 = 30%（距离衰减）
方向 = 东北方向（立体声定位）
    ↓
结果：微弱的枪声，从右前方传来
```

### 场景 3：移动的 AI 开枪
```
AI 边跑边射击
    ↓
音源位置每帧更新（FixedUpdate）
    t=0: (100, 0, 100)
    t=1: (105, 0, 100)
    t=2: (110, 0, 100)
    ...
    ↓
FMOD 实时计算新的距离和方向
    ↓
音效平滑地从远到近/从左到右移动
    ↓
结果：可以通过声音追踪移动的敌人
```

## 与其他游戏音效对比

### 传统 2D 音效游戏
- ❌ 所有音效音量固定
- ❌ 无法判断方向
- ❌ 无距离感
- ❌ 无沉浸感

### FMOD 3D 音效游戏（本游戏）
- ✅ 音量随距离真实衰减
- ✅ 可以听出枪声来源方向
- ✅ 远近枪声音量差异明显
- ✅ 高度沉浸感

## 调试和测试

### 使用 WeaponInfoModule 测试

1. **基础测试**
   ```
   F3 → 玩家 → 武器信息
   点击"🔊 开枪音效 (3D)"
   观察控制台输出：
   [WeaponEffectsPlayer] 🔊 已播放3D音效: SFX/Combat/Gun/Shoot/ak47
       • 音源位置: (10.2, 1.5, 15.3)
       • 附加对象: AK47_Prefab(Clone)
       • 衰减模式: FMOD 3D 空间音效（距离衰减）
   ```

2. **距离测试**
   ```
   1. 点击开枪音效按钮
   2. 向后退5米
   3. 再次点击
   4. 对比音量变化
   ```

3. **方向性测试**
   ```
   1. 戴上耳机
   2. 点击开枪音效
   3. 绕着枪械转圈
   4. 注意左右声道变化
   ```

## 代码示例

### 播放 3D 音效
```csharp
using DuckyNet.Client.Core.Utils;

// 播放枪声（自动使用3D音效）
WeaponEffectsPlayer.PlayShootSound();

// 底层会调用：
// AudioManager.Post("SFX/Combat/Gun/Shoot/ak47", gun.gameObject);
//     ↓
// FMOD 自动处理 3D 空间定位
```

### 其他 3D 音效示例
```csharp
// 脚步声（跟随角色移动）
AudioManager.Post("SFX/Footstep/Concrete", character.gameObject);

// 手榴弹爆炸（固定位置）
AudioManager.Post("SFX/Explosion/Grenade", grenadeObject);

// AI 枪声（跟随 AI 移动）
AudioManager.Post("SFX/Combat/Gun/Shoot/pistol", aiGun.gameObject);
```

## 性能影响

### CPU 开销
- ✅ **低** - FMOD 高度优化
- 每个音源每帧更新：`~0.01ms`
- 监听器更新：`~0.005ms`

### 内存开销
- ✅ **低** - 音效流式加载
- 仅活跃的音效占用内存
- 音效结束后自动释放

## 技术细节

### FMOD 3D 属性结构
```csharp
FMOD.ATTRIBUTES_3D attributes = new FMOD.ATTRIBUTES_3D
{
    position = new FMOD.VECTOR 
    { 
        x = transform.position.x,
        y = transform.position.y,
        z = transform.position.z
    },
    velocity = new FMOD.VECTOR { x = 0, y = 0, z = 0 },
    forward = new FMOD.VECTOR 
    { 
        x = transform.forward.x,
        y = transform.forward.y,
        z = transform.forward.z
    },
    up = new FMOD.VECTOR 
    { 
        x = transform.up.x,
        y = transform.up.y,
        z = transform.up.z
    }
};

eventInstance.set3DAttributes(attributes);
```

### 监听器位置计算
```csharp
// 监听器高度偏移 2 米，模拟耳朵位置
Vector3 listenerPosition = playerPosition + new Vector3(0, 2, 0);

// 监听器朝向 = 相机朝向（第一人称视角）
Quaternion listenerRotation = cameraTransform.rotation;
```

## 相关文件

- **工具类**: `Client/Core/Utils/WeaponEffectsPlayer.cs`
- **调试模块**: `Client/Core/DebugModule/Modules/WeaponInfoModule.cs`
- **游戏源码参考**: 
  - `TeamSoda.Duckov.Core/ItemAgent_Gun.cs` (枪械音效)
  - `TeamSoda.Duckov.Core/Duckov/AudioManager.cs` (音频管理)
  - `TeamSoda.Duckov.Core/Duckov/AudioObject.cs` (3D 音频对象)

