using Services;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>将屏幕输入转换为玩家球面切线，按弧长移动并消耗体力。</summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SphereWalker _walker;
        [SerializeField] private StaminaController _stamina;
        [Header("移动")]
        public float moveSpeed = 8f;
        public bool invertVertical;
        public bool invertHorizontal;

        [Inject] private IInputService _input;
        [Inject] private ICombatPhaseService _combatPhase;
        [Inject] private ICameraMovementBasis _movementBasis;
        private PlayerCombatTarget _combatTarget;

        /// <summary>绑定玩家组件；移动方向由注入的逻辑摄像机基底提供。</summary>
        private void Awake()
        {
            if (_walker == null)
            {
                _walker = GetComponent<SphereWalker>();
            }
            if (_stamina == null)
            {
                _stamina = GetComponent<StaminaController>();
            }
            _combatTarget = GetComponent<PlayerCombatTarget>();
        }

        /// <summary>仅在 Playing 阶段读取输入、推进耐力并按弧长移动。</summary>
        private void Update()
        {
            if (!_combatPhase.CanPerformCombatActions)
            {
                return;
            }

            Vector2 mv = _input.Move;
            float inputX = mv.x;
            float inputY = mv.y;
            bool isMoving = Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputY) > 0.01f;

            _stamina.Tick(Time.deltaTime, isMoving);

            if (!isMoving || !_stamina.canMove)
            {
                return;
            }

            Vector2 input = new Vector2(invertHorizontal ? -inputX : inputX,
                invertVertical ? -inputY : inputY);
            Vector3 normal = (_walker.transform.position - _walker.GetEffectiveCenter()).normalized;
            if (_movementBasis == null)
            {
                return;
            }

            Vector3 direction = _movementBasis.GetMoveDirection(input, normal);
            float speedMultiplier = _combatTarget != null
                ? _combatTarget.GetMovementSpeedMultiplier()
                : 1f;
            _walker.Move(
                direction,
                direction.magnitude * moveSpeed * speedMultiplier * Time.deltaTime);
        }
    }
}
