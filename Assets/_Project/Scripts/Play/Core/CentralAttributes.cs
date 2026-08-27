using System;

namespace Play
{
    public class CentralAttributes
    {
        public float BaseDetectionRadius { get; set; } = 10f;
        public float BaseRotationSpeed { get; set; } = 180f;

        public float DetectionRadius { get; private set; }
        public float RotationSpeed { get; private set; }

        public event Action OnAttributesChanged;

        public CentralAttributes()
        {
            ResetToBase();
        }

        public CentralAttributes(float baseDetectionRadius, float baseRotationSpeed)
        {
            BaseDetectionRadius = baseDetectionRadius;
            BaseRotationSpeed = baseRotationSpeed;
            ResetToBase();
        }

        public void ResetToBase()
        {
            DetectionRadius = BaseDetectionRadius;
            RotationSpeed = BaseRotationSpeed;
            OnAttributesChanged?.Invoke();
        }

        public string GetDescription()
        {
            return $"探测半径: {DetectionRadius:F1}  旋转速度: {RotationSpeed:F0}";
        }
    }
}
