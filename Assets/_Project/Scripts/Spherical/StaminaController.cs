using UnityEngine;

public class StaminaController : MonoBehaviour
{
    [Header("Stamina Config 耐力配置")]
    public float maxStamina = 100f;
    [Tooltip("移动时每秒消耗的耐力")]public float drainRate = 30f;
    [Tooltip("静止时每秒回复的耐力")]public float regenRate = 20f;
    [Tooltip("停止移动后开始回复的延迟秒数")]public float regenDelay = 1f;

    public float currentStamina { get; private set; }
    public bool canMove => currentStamina > 0f;

    public event System.Action<float, float> OnStaminaChanged;

    private float _regenTimer;

    private void Start()
    {
        currentStamina = maxStamina;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    public void Tick(float deltaTime, bool isMoving)
    {
        if (isMoving)
        {
            _regenTimer = 0f;
            if (currentStamina > 0f)
            {
                currentStamina = Mathf.Max(0f, currentStamina - drainRate * deltaTime);
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }
        else
        {
            if (currentStamina >= maxStamina) return;

            _regenTimer += deltaTime;
            if (_regenTimer >= regenDelay)
            {
                float previous = currentStamina;
                currentStamina = Mathf.Min(maxStamina, currentStamina + regenRate * deltaTime);
                if (Mathf.Abs(currentStamina - previous) > 0.001f)
                    OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }
    }
}
