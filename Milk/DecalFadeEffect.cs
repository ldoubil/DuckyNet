using UnityEngine;

namespace Milk
{
    /// <summary>
    /// 贴花渐变消失和扩散效果
    /// </summary>
    public class DecalFadeEffect : MonoBehaviour
    {
        private MeshRenderer? _renderer;
        private Material? _material;
        private float _duration;
        private float _time;
        private float _initialAlpha;
        private Vector3 _initialScale;
        private float _spreadSpeed;
        private float _maxSpreadMultiplier;

        public void Initialize(float duration, float spreadSpeed, float maxSpreadMultiplier)
        {
            _duration = duration;
            _spreadSpeed = spreadSpeed;
            _maxSpreadMultiplier = maxSpreadMultiplier;
        }

        private void Start()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null)
            {
                _material = _renderer.material;
                _initialAlpha = _material != null ? _material.color.a : 1f;
            }
            _initialScale = transform.localScale;
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