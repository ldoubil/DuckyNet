using System.Collections.Generic;
using UnityEngine;

namespace Milk
{
    /// <summary>
    /// 黏糊糊线条创建器
    /// </summary>
    public static class StickyLineCreator
    {
        /// <summary>
        /// 创建黏糊糊拉丝线条
        /// </summary>
        /// <param name="start">起始位置</param>
        /// <param name="end">结束位置</param>
        /// <param name="lineObjects">线条对象列表，用于管理</param>
        public static void CreateStickyLine(Vector3 start, Vector3 end, List<GameObject> lineObjects)
        {
            GameObject lineObj = new GameObject("MilkStickyLine");
            LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();

            // 配置线条渲染器
            ConfigureLineRenderer(lineRenderer);
            
            // 创建自然下垂的曲线
            Vector3[] points = CreateCurvedLine(start, end);
            lineRenderer.SetPositions(points);

            // 设置白色半透明材质
            SetLineMaterial(lineRenderer);

            // 添加动态下垂效果（包含拉伸断裂和变细）
            float distance = Vector3.Distance(start, end);
            var drippingEffect = lineObj.AddComponent<LineDrippingEffect>();
            drippingEffect.Initialize(points, distance, ParticleConfig.LINE_BREAK_DISTANCE_MULTIPLIER, ParticleConfig.LINE_THINNING_SPEED);

            // 自动销毁
            Object.Destroy(lineObj, ParticleConfig.STICKY_LINE_LIFETIME);
            lineObjects.Add(lineObj);

            // 清理已销毁的线条引用
            lineObjects.RemoveAll(obj => obj == null);
        }

        /// <summary>
        /// 配置线条渲染器基本属性
        /// </summary>
        private static void ConfigureLineRenderer(LineRenderer lineRenderer)
        {
            lineRenderer.startWidth = ParticleConfig.LINE_START_WIDTH;
            lineRenderer.endWidth = ParticleConfig.LINE_END_WIDTH;
            lineRenderer.positionCount = ParticleConfig.LINE_SEGMENTS;
            lineRenderer.useWorldSpace = true;
        }

        /// <summary>
        /// 创建自然下垂的曲线点
        /// </summary>
        private static Vector3[] CreateCurvedLine(Vector3 start, Vector3 end)
        {
            Vector3[] points = new Vector3[ParticleConfig.LINE_SEGMENTS];
            float distance = Vector3.Distance(start, end);
            float sagAmount = distance * ParticleConfig.SAG_FACTOR;
            
            for (int i = 0; i < ParticleConfig.LINE_SEGMENTS; i++)
            {
                float t = i / (float)(ParticleConfig.LINE_SEGMENTS - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                
                // 抛物线下垂（模拟重力）
                float sag = sagAmount * Mathf.Sin(t * Mathf.PI);
                point.y -= sag;
                
                // 添加随机摆动（不规则感）
                point += new Vector3(
                    Random.Range(-ParticleConfig.WOBBLE_RANGE, ParticleConfig.WOBBLE_RANGE),
                    Random.Range(-ParticleConfig.WOBBLE_RANGE * 0.67f, ParticleConfig.WOBBLE_RANGE * 0.67f),
                    Random.Range(-ParticleConfig.WOBBLE_RANGE, ParticleConfig.WOBBLE_RANGE)
                );
                
                points[i] = point;
            }
            
            return points;
        }

        /// <summary>
        /// 设置线条材质
        /// </summary>
        private static void SetLineMaterial(LineRenderer lineRenderer)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
                lineRenderer.startColor = new Color(1f, 1f, 1f, 0.7f);  // 起始半透明
                lineRenderer.endColor = new Color(1f, 1f, 1f, 0.4f);    // 结束更透明
            }
        }
    }
}