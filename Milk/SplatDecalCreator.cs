using UnityEngine;

namespace Milk
{
    /// <summary>
    /// 贴花创建器 - 负责在碰撞点创建圆形牛奶贴花
    /// </summary>
    public static class SplatDecalCreator
    {
        /// <summary>
        /// 在碰撞点创建圆形贴花
        /// </summary>
        /// <param name="position">碰撞位置</param>
        /// <param name="normal">表面法线</param>
        public static void CreateSplatDecal(Vector3 position, Vector3 normal)
        {
            GameObject decalObj = new GameObject("MilkSplatDecal");
            decalObj.transform.position = position + normal * ParticleConfig.DECAL_OFFSET;  // 稍微偏移避免 Z-fighting
            decalObj.transform.rotation = Quaternion.LookRotation(-normal);  // 面向法线反方向
            
            var meshFilter = decalObj.AddComponent<MeshFilter>();
            var meshRenderer = decalObj.AddComponent<MeshRenderer>();
            
            // 创建圆形网格
            Mesh mesh = CreateCircularMesh();
            meshFilter.mesh = mesh;
            
            // 创建半透明白色材质
            SetDecalMaterial(meshRenderer);
            
            // 添加渐变消失和扩散效果
            var fadeEffect = decalObj.AddComponent<DecalFadeEffect>();
            fadeEffect.Initialize(ParticleConfig.DECAL_LIFETIME, ParticleConfig.DECAL_SPREAD_SPEED, ParticleConfig.DECAL_MAX_SPREAD_MULTIPLIER);
            
            // 自动销毁
            Object.Destroy(decalObj, ParticleConfig.DECAL_LIFETIME);
        }

        /// <summary>
        /// 创建圆形网格（使用多边形近似）
        /// </summary>
        private static Mesh CreateCircularMesh()
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[ParticleConfig.DECAL_SEGMENTS + 1];
            int[] triangles = new int[ParticleConfig.DECAL_SEGMENTS * 3];
            Vector2[] uvs = new Vector2[ParticleConfig.DECAL_SEGMENTS + 1];
            
            // 中心点
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            
            // 随机半径（不规则大小）
            float radius = Random.Range(ParticleConfig.DECAL_RADIUS_MIN, ParticleConfig.DECAL_RADIUS_MAX);
            
            // 生成圆形顶点
            for (int i = 0; i < ParticleConfig.DECAL_SEGMENTS; i++)
            {
                float angle = (i / (float)ParticleConfig.DECAL_SEGMENTS) * Mathf.PI * 2f;
                float irregularity = Random.Range(ParticleConfig.DECAL_IRREGULARITY_MIN, ParticleConfig.DECAL_IRREGULARITY_MAX);
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
                
                // 三角形索引
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % ParticleConfig.DECAL_SEGMENTS + 1;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            
            return mesh;
        }

        /// <summary>
        /// 设置贴花材质
        /// </summary>
        private static void SetDecalMaterial(MeshRenderer meshRenderer)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                var material = new Material(shader);
                material.color = new Color(1f, 1f, 1f, ParticleConfig.DECAL_ALPHA);
                meshRenderer.material = material;
            }
        }
    }
}