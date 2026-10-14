using Play.FieldContact.Implement;
using Play.FieldContact.Model;
using UnityEngine;

namespace Play.FieldContact.View
{
    /// <summary>按材质目标模式把附近 Mesh 接触数据应用到球面 Field 的材质属性块。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer), typeof(SphereCollider))]
    public sealed class FieldContactView : MonoBehaviour
    {
        private static readonly int ContactCountId = Shader.PropertyToID("_ContactCount");
        private static readonly int ContactDataId = Shader.PropertyToID("_ContactData");
        private static readonly int ContactDistanceId = Shader.PropertyToID("_ContactDistance");
        private static readonly int OnlyEnemyId = Shader.PropertyToID("_OnlyEnemy");

        [SerializeField] private MeshRenderer _fieldRenderer;
        [SerializeField] private SphereCollider _fieldCollider;
        [SerializeField] private LayerMask _targetLayers = -1;

        private readonly FieldContactSample[] _contacts =
            new FieldContactSample[FieldContactQuery.ContactCapacity];

        private readonly Vector4[] _shaderContacts =
            new Vector4[FieldContactQuery.ContactCapacity];

        private FieldContactQuery _query;
        private MaterialPropertyBlock _properties;
        private Material _fieldMaterial;
        private float _revealDistance;
        private bool _isVisible;

        /// <summary>缓存 Field 组件并保持无接触时默认隐藏。</summary>
        private void Awake()
        {
            if (_fieldRenderer == null)
            {
                _fieldRenderer = GetComponent<MeshRenderer>();
            }

            if (_fieldCollider == null)
            {
                _fieldCollider = GetComponent<SphereCollider>();
            }

            if (_fieldRenderer == null || _fieldCollider == null)
            {
                enabled = false;
                return;
            }

            _query = new FieldContactQuery();
            _properties = new MaterialPropertyBlock();

            _fieldMaterial = _fieldRenderer.sharedMaterial;
            _revealDistance = _fieldMaterial != null && _fieldMaterial.HasProperty(ContactDistanceId)
                ? _fieldMaterial.GetFloat(ContactDistanceId)
                : 0f;

            _fieldRenderer.enabled = false;
            _isVisible = false;
        }

        /// <summary>查询接触源并更新局部球面显现遮罩。</summary>
        private void LateUpdate()
        {
            if (_fieldMaterial != null)
            {
                _revealDistance = _fieldMaterial.GetFloat(ContactDistanceId);
            }

            if (_revealDistance <= 0f)
            {
                SetVisible(false);
                return;
            }

            int contactCount = _query.Collect(
                transform,
                _fieldCollider,
                _targetLayers,
                GetTargetMode(),
                _revealDistance,
                _contacts);

            if (contactCount == 0)
            {
                SetContactCount(0);
                SetVisible(false);
                return;
            }

            for (int index = 0; index < _shaderContacts.Length; index++)
            {
                if (index < contactCount)
                {
                    FieldContactSample contact = _contacts[index];
                    _shaderContacts[index] = new Vector4(
                        contact.Direction.x,
                        contact.Direction.y,
                        contact.Direction.z,
                        contact.Strength);
                }
                else
                {
                    _shaderContacts[index] = Vector4.zero;
                }
            }

            _fieldRenderer.GetPropertyBlock(_properties);
            _properties.SetFloat(ContactCountId, contactCount);
            _properties.SetVectorArray(ContactDataId, _shaderContacts);
            _fieldRenderer.SetPropertyBlock(_properties);
            SetVisible(true);
        }

        /// <summary>根据材质开关选择接触目标过滤模式。</summary>
        /// <returns>当前接触目标模式。</returns>
        private FieldContactTargetMode GetTargetMode()
        {
            if (_fieldMaterial == null ||
                !_fieldMaterial.HasProperty(OnlyEnemyId) ||
                _fieldMaterial.GetFloat(OnlyEnemyId) > 0.5f)
            {
                return FieldContactTargetMode.EnemyOnly;
            }

            return FieldContactTargetMode.All;
        }

        /// <summary>关闭组件时隐藏其球面渲染器。</summary>
        private void OnDisable()
        {
            SetVisible(false);
        }

        /// <summary>切换 Field Renderer，只在状态变化时写入。</summary>
        /// <param name="visible">是否绘制球面 Field。</param>
        private void SetVisible(bool visible)
        {
            if (_fieldRenderer == null || _isVisible == visible)
            {
                return;
            }

            _isVisible = visible;
            _fieldRenderer.enabled = visible;
        }

        /// <summary>将接触数量写入材质属性块，清除上帧的残留状态。</summary>
        /// <param name="count">本帧的接触源数量。</param>
        private void SetContactCount(int count)
        {
            _fieldRenderer.GetPropertyBlock(_properties);
            _properties.SetFloat(ContactCountId, count);
            _fieldRenderer.SetPropertyBlock(_properties);
        }
    }
}
