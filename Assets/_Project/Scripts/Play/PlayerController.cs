using Services;
using UnityEngine;
using VContainer;

namespace Play
{
    public class PlayerController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private SphereWalker _walker;
        [SerializeField] private StaminaController _stamina;

        [Header("移动")]
        public float moveSpeed = 8f;
        public bool invertLatitude;
        public bool invertLongitude;

        [Inject] private IInputService _input;

        private void Awake()
        {
            if (_walker == null) _walker = GetComponent<SphereWalker>();
            if (_stamina == null) _stamina = GetComponent<StaminaController>();
        }

        private void Update()
        {
            Vector2 mv = _input != null ? _input.Move : Vector2.zero;
            float inputX = mv.x;
            float inputY = mv.y;
            bool isMoving = Mathf.Abs(inputX) > 0.01f || Mathf.Abs(inputY) > 0.01f;

            _stamina.Tick(Time.deltaTime, isMoving);

            if (!isMoving || !_stamina.canMove) return;

            float angularSpeed = moveSpeed / _walker.radius;//角速度
            float cosLat = Mathf.Cos(_walker.latitude);
            if (cosLat < 1e-4f) cosLat = 1e-4f;

            float lonSign = invertLongitude ? -1f : 1f;
            float deltaLon = inputX * lonSign * angularSpeed * Time.deltaTime / cosLat;
            float latSign = invertLatitude ? -1f : 1f;
            float deltaLat = inputY * latSign * angularSpeed * Time.deltaTime;

            _walker.Move(deltaLon, deltaLat);
        }
    }

}
