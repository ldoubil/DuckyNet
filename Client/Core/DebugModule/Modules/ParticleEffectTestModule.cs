
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 粒子特效测试模块
    /// 用于测试各种粒子特效，如牛奶喷溅等
    /// </summary>
    public class ParticleEffectTestModule : IDebugModule
    {
        public string ModuleName => "粒子特效测试";
        public string Category => "测试";
        public string Description => "测试各种粒子特效（牛奶喷溅、抛物线等）";
        public bool IsEnabled { get; set; } = true;

        private GameObject? _milkParticleSystem;
        private float _shootForce = 10f;  // 初始速度
        private float _gravity = 1.0f;    // 正常重力
        private int _particleCount = 150; // 粒子数量

        public void OnGUI()
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label("═══ 牛奶粒子特效 ═══", new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            });

            GUILayout.Space(10);

            // 参数设置
            GUILayout.Label("发射参数:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

            GUILayout.BeginHorizontal();
            GUILayout.Label("发射力度:", GUILayout.Width(80));
            if (float.TryParse(GUILayout.TextField(_shootForce.ToString("F1"), GUILayout.Width(60)), out float force))
                _shootForce = force;
            GUILayout.Label($"({_shootForce:F1})", GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("重力倍数:", GUILayout.Width(80));
            if (float.TryParse(GUILayout.TextField(_gravity.ToString("F2"), GUILayout.Width(60)), out float grav))
                _gravity = grav;
            GUILayout.Label($"({_gravity:F2})", GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("粒子数量:", GUILayout.Width(80));
            if (int.TryParse(GUILayout.TextField(_particleCount.ToString(), GUILayout.Width(60)), out int count))
                _particleCount = count;
            GUILayout.Label($"({_particleCount})", GUILayout.Width(60));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 发射按钮
            GUI.backgroundColor = new Color(1f, 0.9f, 0.9f);
            if (GUILayout.Button("🥛 发射牛奶粒子", GUILayout.Height(40)))
            {
                ShootMilkParticles();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // 快速测试按钮
            GUILayout.Label("快速测试:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("轻力度"))
            {
                _shootForce = 5f;
                ShootMilkParticles();
            }
            if (GUILayout.Button("中力度"))
            {
                _shootForce = 10f;
                ShootMilkParticles();
            }
            if (GUILayout.Button("大力度"))
            {
                _shootForce = 20f;
                ShootMilkParticles();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 清理按钮
            if (_milkParticleSystem != null)
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("清理所有粒子"))
                {
                    CleanupParticles();
                }
                GUI.backgroundColor = Color.white;
            }

            // 清理所有黏糊糊线条
            GUILayout.Space(5);
            GUI.backgroundColor = new Color(1f, 0.5f, 0f);
            if (GUILayout.Button("清理所有黏糊糊线条"))
            {
                CleanupAllStickyLines();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 发射牛奶粒子特效
        /// </summary>
        private void ShootMilkParticles()
        {
            try
            {
                var localPlayer = GameContext.Instance?.PlayerManager?.LocalPlayer;
                if (localPlayer?.CharacterObject == null)
                {
                    Debug.LogWarning("[ParticleEffectTest] 本地玩家角色不存在");
                    return;
                }

                var characterTransform = localPlayer.CharacterObject.transform;
                
                // 计算发射位置（玩家前方 1.5 米，高度 1.5 米）
                Vector3 shootPosition = characterTransform.position + 
                                       characterTransform.forward * 1.5f + 
                                       Vector3.up * 1.5f;
                
                // 发射方向（前方稍微向上）
                Vector3 shootDirection = (characterTransform.forward + Vector3.up * 0.3f).normalized;

                // 创建粒子系统
                CreateMilkParticleSystem(shootPosition, shootDirection);

                Debug.Log($"[ParticleEffectTest] ✅ 牛奶粒子已发射！位置: {shootPosition}, 方向: {shootDirection}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ParticleEffectTest] 发射粒子失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建牛奶粒子系统
        /// </summary>
        private void CreateMilkParticleSystem(Vector3 position, Vector3 direction)
        {
            // 创建游戏对象
            var particleObj = new GameObject("MilkParticleEffect");
            particleObj.transform.position = position;
            particleObj.transform.rotation = Quaternion.LookRotation(direction);

            // 添加粒子系统组件
            var ps = particleObj.AddComponent<ParticleSystem>();
            
            // 添加黏糊糊效果处理器
            particleObj.AddComponent<MilkStickyEffectHandler>();
            
            var main = ps.main;
            
            // 主模块设置（简化但可见）
            main.duration = 0.2f;                   // 短时间爆发
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 3.0f);  // 随机生命周期（调整为1-3秒）
            main.startSpeed = new ParticleSystem.MinMaxCurve(_shootForce * 0.8f, _shootForce * 1.2f);  // 随机初始速度
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);    // 更大的粒子（0.15-0.35米）
            main.startColor = Color.white;          // 纯白色
            main.gravityModifier = new ParticleSystem.MinMaxCurve(_gravity * 0.8f, _gravity * 1.5f);  // 减小重力范围
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = _particleCount * 2;

            // 发射模块（立即发射）
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            
            // 使用 Burst 立即发射
            ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, _particleCount);
            emission.SetBurst(0, burst);

            // 形状模块（锥形，增大角度让粒子更分散）
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;                      // 更大的发射角度
            shape.radius = 0.1f;
            shape.radiusThickness = 0.5f;           // 从锥形边缘发射

            // 生命周期内旋转（液滴翻滚效果）
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

            // 渲染模块（创建正确的白色材质）
            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 100;
                
                // 尝试找到合适的 Shader
                Shader? particleShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended") 
                                       ?? Shader.Find("Particles/Alpha Blended Premultiply")
                                       ?? Shader.Find("Particles/Alpha Blended")
                                       ?? Shader.Find("Mobile/Particles/Alpha Blended")
                                       ?? Shader.Find("Sprites/Default");
                
                if (particleShader != null)
                {
                    Material milkMaterial = new Material(particleShader);
                    milkMaterial.color = Color.white;
                    
                    // 如果有 _TintColor 属性（老版本粒子 Shader）
                    if (milkMaterial.HasProperty("_TintColor"))
                    {
                        milkMaterial.SetColor("_TintColor", Color.white);
                    }
                    
                    renderer.material = milkMaterial;
                    renderer.trailMaterial = new Material(milkMaterial);  // 拖尾也使用相同材质
                    
                    Debug.Log($"[ParticleEffectTest] ✅ 已创建白色粒子材质: {particleShader.name}");
                }
                else
                {
                    Debug.LogWarning($"[ParticleEffectTest] ⚠️ 未找到合适的粒子 Shader");
                    // 尝试使用默认材质
                    if (renderer.material != null)
                    {
                        renderer.material.color = Color.white;
                        Debug.Log($"[ParticleEffectTest] 使用默认材质: {renderer.material.shader.name}");
                    }
                }
            }

            // 碰撞模块（基础设置 + 发送碰撞消息）
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = 0.5f;                // 中等阻尼
            collision.bounce = 0.3f;                // 一点反弹
            collision.lifetimeLoss = 0.1f;
            collision.collidesWith = ~0;
            collision.sendCollisionMessages = true; // 启用碰撞消息（用于黏糊糊效果）

            // 拖尾模块（基础液体拖尾）
            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 1.0f;
            trails.lifetime = 0.3f;                 // 快速消失的拖尾
            trails.minVertexDistance = 0.02f;
            trails.worldSpace = true;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;
            trails.inheritParticleColor = true;

            // 自动销毁
            GameObject.Destroy(particleObj, 10f);

            _milkParticleSystem = particleObj;

            // 立即播放并发射粒子
            ps.Play();
            
            // 手动发射粒子（确保一定有粒子）
            ps.Emit(_particleCount);

            Debug.Log($"[ParticleEffectTest] ✅ 创建并播放粒子：位置={position}, 发射={_particleCount}个, 当前粒子数={ps.particleCount}");
        }


        /// <summary>
        /// 清理所有粒子
        /// </summary>
        private void CleanupParticles()
        {
            if (_milkParticleSystem != null)
            {
                GameObject.Destroy(_milkParticleSystem);
                _milkParticleSystem = null;
                Debug.Log("[ParticleEffectTest] 已清理粒子系统");
            }
        }

        /// <summary>
        /// 清理所有黏糊糊线条
        /// </summary>
        private void CleanupAllStickyLines()
        {
            GameObject[] allLines = GameObject.FindObjectsOfType<GameObject>();
            int count = 0;
            foreach (var obj in allLines)
            {
                if (obj.name == "MilkStickyLine")
                {
                    GameObject.Destroy(obj);
                    count++;
                }
            }
            Debug.Log($"[ParticleEffectTest] 已清理 {count} 条黏糊糊线条");
        }

        public void Update()
        {
            // 不需要每帧更新
        }
    }

    /// <summary>
    /// 粒子碰撞监听器 - 创建黏糊糊的拉丝效果（优化版）
    /// </summary>
    public class MilkStickyEffectHandler : MonoBehaviour
    {
        private List<CollisionPointInfo> _collisionPoints = new List<CollisionPointInfo>();
        private List<GameObject> _lineObjects = new List<GameObject>();
        private ParticleSystem? _ps;
        private List<ParticleCollisionEvent> _collisionEvents = new List<ParticleCollisionEvent>();
        private int _maxLines = 30;  // 增加最大线条数量
        private float _minDistance = 0.3f;  // 减小最小距离，允许更多连线
        private float _createChance = 0.4f;  // 提高创建概率到40%
        
        private class CollisionPointInfo
        {
            public Vector3 Position;
            public GameObject? HitObject;
            public float Time;
        }

        private void Start()
        {
            _ps = GetComponent<ParticleSystem>();
        }

        private void OnParticleCollision(GameObject other)
        {
            if (_ps == null) return;

            // 获取碰撞事件
            int numCollisionEvents = _ps.GetCollisionEvents(other, _collisionEvents);

            for (int i = 0; i < numCollisionEvents; i++)
            {
                Vector3 collisionPoint = _collisionEvents[i].intersection;
                Vector3 normal = _collisionEvents[i].normal;
                
                // 创建碰撞贴花（每次都创建）
                CreateSplatDecal(collisionPoint, normal);
                
                // 添加碰撞点信息
                var pointInfo = new CollisionPointInfo
                {
                    Position = collisionPoint,
                    HitObject = other,
                    Time = Time.time
                };
                _collisionPoints.Add(pointInfo);

                // 创建多条连线（增加立体感）
                if (_collisionPoints.Count >= 2 && _lineObjects.Count < _maxLines)
                {
                    // 1. 连接到最近的几个点（不同物体或同物体）
                    var nearbyPoints = _collisionPoints
                        .Where(p => p != pointInfo && Vector3.Distance(p.Position, collisionPoint) > _minDistance)
                        .OrderBy(p => Vector3.Distance(p.Position, collisionPoint))
                        .Take(3)  // 连接到最近的3个点
                        .ToList();

                    foreach (var nearPoint in nearbyPoints)
                    {
                        if (_lineObjects.Count >= _maxLines) break;
                        
                        float distance = Vector3.Distance(nearPoint.Position, collisionPoint);
                        
                        // 随机创建连线
                        if (UnityEngine.Random.value < _createChance && distance < 3f)
                        {
                            CreateStickyLine(nearPoint.Position, collisionPoint);
                        }
                    }
                    
                    // 2. 有概率连接同一物体上的点（自身粘连）
                    if (UnityEngine.Random.value < 0.3f)  // 30%概率
                    {
                        var sameObjectPoints = _collisionPoints
                            .Where(p => p.HitObject == other && p != pointInfo)
                            .OrderBy(p => Vector3.Distance(p.Position, collisionPoint))
                            .Take(2)
                            .ToList();
                            
                        foreach (var samePoint in sameObjectPoints)
                        {
                            if (_lineObjects.Count >= _maxLines) break;
                            
                            float distance = Vector3.Distance(samePoint.Position, collisionPoint);
                            if (distance > _minDistance && distance < 2f)
                            {
                                CreateStickyLine(samePoint.Position, collisionPoint);
                            }
                        }
                    }
                }

                // 限制碰撞点数量
                if (_collisionPoints.Count > 50)
                {
                    _collisionPoints.RemoveAt(0);
                }
            }
        }

        private void CreateStickyLine(Vector3 start, Vector3 end)
        {
            // 创建线条对象
            GameObject lineObj = new GameObject("MilkStickyLine");
            LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();

            // 设置更粗的线条属性
            lineRenderer.startWidth = 0.12f;  // 更粗的起始宽度
            lineRenderer.endWidth = 0.06f;    // 更粗的结束宽度
            lineRenderer.positionCount = 8;   // 更多点以实现流畅的下垂曲线
            lineRenderer.useWorldSpace = true;
            
            // 创建自然下垂的曲线（使用抛物线）
            Vector3[] points = new Vector3[8];
            float distance = Vector3.Distance(start, end);
            float sagAmount = distance * 0.3f;  // 下垂量为距离的30%
            
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                Vector3 point = Vector3.Lerp(start, end, t);
                
                // 抛物线下垂 (模拟重力效果)
                float sag = sagAmount * Mathf.Sin(t * Mathf.PI);
                point.y -= sag;
                
                // 添加一点随机摆动（黏糊糊的不规则感）
                point += new Vector3(
                    UnityEngine.Random.Range(-0.03f, 0.03f),
                    UnityEngine.Random.Range(-0.02f, 0.02f),
                    UnityEngine.Random.Range(-0.03f, 0.03f)
                );
                
                points[i] = point;
            }
            
            lineRenderer.SetPositions(points);

            // 使用白色半透明材质
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
                lineRenderer.startColor = new Color(1f, 1f, 1f, 0.7f);
                lineRenderer.endColor = new Color(1f, 1f, 1f, 0.4f);
            }

            // 添加动态下垂组件
            var drippingEffect = lineObj.AddComponent<LineDrippingEffect>();
            drippingEffect.Initialize(points);

            // 8秒后销毁
            Destroy(lineObj, 8f);
            _lineObjects.Add(lineObj);

            // 清理已销毁的线条引用
            _lineObjects.RemoveAll(obj => obj == null);
        }
        
        /// <summary>
        /// 在碰撞点创建圆形贴花
        /// </summary>
        private void CreateSplatDecal(Vector3 position, Vector3 normal)
        {
            GameObject decalObj = new GameObject("MilkSplatDecal");
            decalObj.transform.position = position + normal * 0.01f;  // 稍微偏移避免 Z-fighting
            decalObj.transform.rotation = Quaternion.LookRotation(-normal);  // 面向法线反方向
            
            // 创建四边形作为贴花
            var meshFilter = decalObj.AddComponent<MeshFilter>();
            var meshRenderer = decalObj.AddComponent<MeshRenderer>();
            
            // 创建圆形网格（使用多边形近似）
            Mesh mesh = new Mesh();
            int segments = 16;
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            Vector2[] uvs = new Vector2[segments + 1];
            
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            
            float radius = UnityEngine.Random.Range(0.2f, 0.4f);  // 随机大小
            
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float irregularity = UnityEngine.Random.Range(0.8f, 1.2f);  // 不规则边缘
                float r = radius * irregularity;
                
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r,
                    0
                );
                uvs[i + 1] = new Vector2(
                    Mathf.Cos(angle) * 0.5f + 0.5f,
                    Mathf.Sin(angle) * 0.5f + 0.5f
                );
                
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            meshFilter.mesh = mesh;
            
            // 创建半透明白色材质
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                var material = new Material(shader);
                material.color = new Color(1f, 1f, 1f, 0.8f);
                meshRenderer.material = material;
            }
            
            // 添加渐变消失组件
            var fadeEffect = decalObj.AddComponent<DecalFadeEffect>();
            fadeEffect.Initialize(6f);  // 6秒后完全消失
            
            Destroy(decalObj, 6f);
            _lineObjects.Add(decalObj);
        }

        private void OnDestroy()
        {
            // 清理所有线条
            foreach (var lineObj in _lineObjects)
            {
                if (lineObj != null)
                {
                    Destroy(lineObj);
                }
            }
            _lineObjects.Clear();
        }
    }

    /// <summary>
    /// 线条动态下垂效果
    /// </summary>
    public class LineDrippingEffect : MonoBehaviour
    {
        private LineRenderer? _lineRenderer;
        private Vector3[] _originalPoints = Array.Empty<Vector3>();
        private float _time;
        private float _drippingSpeed = 0.5f;

        public void Initialize(Vector3[] points)
        {
            _originalPoints = new Vector3[points.Length];
            Array.Copy(points, _originalPoints, points.Length);
        }

        private void Start()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        private void Update()
        {
            if (_lineRenderer == null || _originalPoints.Length == 0) return;

            _time += Time.deltaTime;
            
            // 逐渐下垂
            for (int i = 0; i < _originalPoints.Length; i++)
            {
                float t = i / (float)(_originalPoints.Length - 1);
                Vector3 point = _originalPoints[i];
                
                // 中间部分下垂更多
                float sagFactor = Mathf.Sin(t * Mathf.PI);
                point.y -= _time * _drippingSpeed * sagFactor;
                
                _lineRenderer.SetPosition(i, point);
            }
        }
    }

    /// <summary>
    /// 贴花渐变消失效果
    /// </summary>
    public class DecalFadeEffect : MonoBehaviour
    {
        private MeshRenderer? _renderer;
        private Material? _material;
        private float _duration;
        private float _time;
        private float _initialAlpha;

        public void Initialize(float duration)
        {
            _duration = duration;
        }

        private void Start()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null)
            {
                _material = _renderer.material;
                _initialAlpha = _material != null ? _material.color.a : 1f;
            }
        }

        private void Update()
        {
            if (_material == null) return;

            _time += Time.deltaTime;
            float alpha = _initialAlpha * (1f - _time / _duration);
            
            Color color = _material.color;
            color.a = Mathf.Max(0, alpha);
            _material.color = color;
        }
    }
}

