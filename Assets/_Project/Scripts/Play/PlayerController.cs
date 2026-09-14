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
        [Inject]
        [SerializeField] private Camera _movementCamera;

        [Header("移动")]
        public float moveSpeed = 8f;
        public bool invertVertical;
        public bool invertHorizontal;

        [Inject] private IInputService _input;
        private PlayerCombatTarget _combatTarget;

        /// <summary>绑定玩家组件及实际输出画面的主相机。</summary>
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
            if (_movementCamera == null)
            {
                _movementCamera = Camera.main;
            }
        }

        /// <summary>按输入强度计算本帧弧长；画面方向在两极仍保持连续。</summary>
        private void Update()
        {
            Vector2 mv = _input != null ? _input.Move : Vector2.zero;
            float inputX = mv.x;
            float inputY = mv.y;
            bool isMoving = Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputY) > 0.01f;

            if (_movementCamera == null)
            {
                _movementCamera = Camera.main;
            }

            _stamina.Tick(Time.deltaTime, isMoving);

            if (!isMoving || !_stamina.canMove || _movementCamera == null)
            {
                return;
            }

            Vector2 input = new Vector2(invertHorizontal ? -inputX : inputX,
                invertVertical ? -inputY : inputY);
            Vector3 normal = (_walker.transform.position - _walker.GetEffectiveCenter()).normalized;
            Vector3 direction = GetMoveDirection(input, normal, _movementCamera.transform.rotation);
            float speedMultiplier = _combatTarget != null
                ? _combatTarget.GetMovementSpeedMultiplier()
                : 1f;
            _walker.Move(
                direction,
                direction.magnitude * moveSpeed * speedMultiplier * Time.deltaTime);
        }

        /// <summary>将镜头平面旋转至球面切平面，保留正交方向与模拟输入强度。</summary>
        /// <param name="input">屏幕左右、上下输入。</param>
        /// <param name="normal">玩家所在球面的单位法线。</param>
        /// <param name="cameraRotation">当前输出镜头的世界朝向。</param>
        /// <returns>长度不超过一的世界切线。</returns>
        private static Vector3 GetMoveDirection(Vector2 input, Vector3 normal, Quaternion cameraRotation)
        {
            Vector2 clamped = Vector2.ClampMagnitude(input, 1f);
            Quaternion alignment = Quaternion.FromToRotation(cameraRotation * Vector3.back, normal);
            return alignment * (cameraRotation * new Vector3(clamped.x, clamped.y, 0f));
        }
    }

}
