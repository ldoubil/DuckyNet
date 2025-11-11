using System;
using UnityEngine;

namespace Milk
{
    /// <summary>
    /// 线条动态下垂、拉伸变细和断裂效果
    /// </summary>
    public class LineDrippingEffect : MonoBehaviour
    {
        private LineRenderer? _lineRenderer;
        private Vector3[] _originalPoints = Array.Empty<Vector3>();
        private float _time;
        private float _originalDistance;
        private float _breakDistanceMultiplier;
        private float _thinningSpeed;
        private float _initialStartWidth;
        private float _initialEndWidth;

        public void Initialize(Vector3[] points, float originalDistance, float breakMultiplier, float thinningSpeed)
        {
            _originalPoints = new Vector3[points.Length];
            Array.Copy(points, _originalPoints, points.Length);
            _originalDistance = originalDistance;
            _breakDistanceMultiplier = breakMultiplier;
            _thinningSpeed = thinningSpeed;
        }

        private void Start()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer != null)
            {
                _initialStartWidth = _lineRenderer.startWidth;
                _initialEndWidth = _lineRenderer.endWidth;
            }
        }

        private void Update()
        {
            if (_lineRenderer == null || _originalPoints.Length == 0) return;

            _time += Time.deltaTime;
            
            // 计算当前线条的实际长度
            Vector3 startPoint = _originalPoints[0];
            Vector3 endPoint = _originalPoints[_originalPoints.Length - 1];
            float currentDistance = Vector3.Distance(_lineRenderer.GetPosition(0), 
                                                      _lineRenderer.GetPosition(_originalPoints.Length - 1));
            
            // 如果拉伸超过原长度的倍数，线条断裂（销毁）
            if (currentDistance > _originalDistance * _breakDistanceMultiplier)
            {
                Destroy(gameObject);
                return;
            }
            
            // 逐渐下垂（中间部分下垂更多）
            for (int i = 0; i < _originalPoints.Length; i++)
            {
                float t = i / (float)(_originalPoints.Length - 1);
                Vector3 point = _originalPoints[i];
                
                // 使用正弦函数让中间部分下垂更多
                float sagFactor = Mathf.Sin(t * Mathf.PI);
                point.y -= _time * ParticleConfig.DRIPPING_SPEED * sagFactor;
                
                _lineRenderer.SetPosition(i, point);
            }
            
            // 随时间逐渐变细（拉伸效果）
            float thinningFactor = 1f - (_time * _thinningSpeed);
            thinningFactor = Mathf.Max(0.2f, thinningFactor);  // 最细不低于 20%
            
            _lineRenderer.startWidth = _initialStartWidth * thinningFactor;
            _lineRenderer.endWidth = _initialEndWidth * thinningFactor;
        }
    }
}