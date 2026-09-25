using R3;
using UnityEngine;

/// <summary>
/// 管理玩家耐力的消耗、恢复和可观察状态。
/// 玩法逻辑通过当前耐力属性读取状态，表现层通过 R3 只读属性接收变化。
/// </summary>
public class StaminaController : MonoBehaviour
{
    [Header("Stamina Config 耐力配置")]
    public float maxStamina = 100f;
    [Tooltip("移动时每秒消耗的耐力")]
    public float drainRate = 30f;
    [Tooltip("静止时每秒回复的耐力")]
    public float regenRate = 20f;
    [Tooltip("停止移动后开始回复的延迟秒数")]
    public float regenDelay = 1f;

    private readonly ReactiveProperty<float> _currentStaminaValue = new();
    private float _regenTimer;

    public float currentStamina { get; private set; }
    public bool canMove => currentStamina > 0f;
    public ReadOnlyReactiveProperty<float> CurrentStamina => _currentStaminaValue;

    private void Start()
    {
        currentStamina = maxStamina;
        PublishStamina();
    }

    /// <summary>
    /// 根据玩家移动状态推进耐力；数值变化通过 R3 状态通知表现层。
    /// </summary>
    /// <param name="deltaTime">本次推进经过的秒数。</param>
    /// <param name="isMoving">是否处于移动状态。</param>
    public void Tick(float deltaTime, bool isMoving)
    {
        if (isMoving)
        {
            _regenTimer = 0f;
            if (currentStamina > 0f)
            {
                currentStamina = Mathf.Max(
                    0f,
                    currentStamina - drainRate * deltaTime);
                PublishStamina();
            }

            return;
        }

        if (currentStamina >= maxStamina)
        {
            return;
        }

        _regenTimer += deltaTime;
        if (_regenTimer < regenDelay)
        {
            return;
        }

        float previous = currentStamina;
        currentStamina = Mathf.Min(
            maxStamina,
            currentStamina + regenRate * deltaTime);
        if (Mathf.Abs(currentStamina - previous) > 0.001f)
        {
            PublishStamina();
        }
    }

    private void PublishStamina()
    {
        _currentStaminaValue.Value = currentStamina;
    }

    private void OnDestroy()
    {
        _currentStaminaValue.Dispose();
    }
}
