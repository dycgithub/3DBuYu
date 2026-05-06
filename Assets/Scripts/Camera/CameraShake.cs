using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// 摄像机震动效果
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("震动设置")]
        [Tooltip("默认震动时长")]
        public float defaultDuration = 0.3f;

        [Tooltip("默认震动强度")]
        public float defaultMagnitude = 0.3f;

        // 震动状态
        private Vector3 originalPosition;
        private float currentShakeDuration = 0f;
        private float currentShakeMagnitude = 0f;
        private bool isShaking = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnEnable()
        {
            originalPosition = transform.localPosition;
        }

        void Update()
        {
            if (isShaking)
            {
                if (currentShakeDuration > 0)
                {
                    // 生成随机偏移
                    Vector3 randomOffset = Random.insideUnitSphere * currentShakeMagnitude;
                    randomOffset.z = 0; // 保持Z轴不变(2D游戏)

                    transform.localPosition = originalPosition + randomOffset;

                    currentShakeDuration -= Time.deltaTime;
                }
                else
                {
                    // 震动结束
                    StopShake();
                }
            }
        }

        /// <summary>
        /// 开始摄像机震动
        /// </summary>
        /// <param name="duration">震动时长</param>
        /// <param name="magnitude">震动强度</param>
        public void Shake(float duration, float magnitude)
        {
            currentShakeDuration = duration;
            currentShakeMagnitude = magnitude;
            isShaking = true;

            // 保存原始位置
            if (!isShaking)
            {
                originalPosition = transform.localPosition;
            }
        }

        /// <summary>
        /// 使用默认参数震动
        /// </summary>
        public void Shake()
        {
            Shake(defaultDuration, defaultMagnitude);
        }

        /// <summary>
        /// 停止震动
        /// </summary>
        public void StopShake()
        {
            isShaking = false;
            currentShakeDuration = 0f;
            transform.localPosition = originalPosition;
        }
    }
}
