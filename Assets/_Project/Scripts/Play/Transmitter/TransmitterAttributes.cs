using System;
using System.Text;

namespace Play
{
    /// <summary>
    /// 单个发射器的运行时属性。
    /// 该类只保存数据和重置逻辑，不创建 Unity 对象，也不依赖控制器。
    /// </summary>
    public class TransmitterAttributes
    {
        public enum PortAttributesType
        {
            Damage,
            Range,
            FireRate,
            ProjectileCount,
            CriticalChance,
            CriticalDamage,
            Penetration,
            TrackingSpeed
        }

        private const float DefaultDamage = 10f;
        private const float DefaultRange = 5f;
        private const float DefaultFireRate = 1f;
        private const int DefaultProjectileCount = 1;
        private const float DefaultCriticalChance = 0.05f;
        private const float DefaultCriticalDamage = 1.5f;
        private const int DefaultPenetration = 0;
        private const float DefaultTrackingSpeed = 10f;

        private float _baseDamage;
        private float _baseRange;
        private float _baseFireRate;
        private int _baseProjectileCount;
        private float _baseCriticalChance;
        private float _baseCriticalDamage;
        private int _basePenetration;
        private float _baseTrackingSpeed;

        private float _damage;
        private float _range;
        private float _fireRate;
        private int _projectileCount;
        private float _criticalChance;
        private float _criticalDamage;
        private int _penetration;
        private float _trackingSpeed;

        public float BaseDamage { get => _baseDamage; set => _baseDamage = value; }
        public float BaseRange { get => _baseRange; set => _baseRange = value; }
        public float BaseFireRate { get => _baseFireRate; set => _baseFireRate = value; }
        public int BaseProjectileCount { get => _baseProjectileCount; set => _baseProjectileCount = value; }
        public float BaseCriticalChance { get => _baseCriticalChance; set => _baseCriticalChance = value; }
        public float BaseCriticalDamage { get => _baseCriticalDamage; set => _baseCriticalDamage = value; }
        public int BasePenetration { get => _basePenetration; set => _basePenetration = value; }
        public float BaseTrackingSpeed { get => _baseTrackingSpeed; set => _baseTrackingSpeed = value; }

        public float Damage => _damage;
        public float Range => _range;
        public float FireRate => _fireRate;
        public int ProjectileCount => _projectileCount;
        public float CriticalChance => _criticalChance;
        public float CriticalDamage => _criticalDamage;
        public int Penetration => _penetration;
        public float TrackingSpeed => _trackingSpeed;

        public bool HasHoming { get; private set; }
        public float HomingStrength { get; private set; }
        public bool HasAreaDamage { get; private set; }
        public float AreaDamageRadius { get; private set; }

        public event Action OnAttributesChanged;

        public TransmitterAttributes()
        {
            SetDefaultBaseValues();
            ResetToBase();
        }

        public TransmitterAttributes(TransmitterAttributes template)
        {
            if (template == null)
                SetDefaultBaseValues();
            else
                CopyBaseValues(template);

            ResetToBase();
        }

        private void CopyBaseValues(TransmitterAttributes source)
        {
            _baseDamage = source._baseDamage;
            _baseRange = source._baseRange;
            _baseFireRate = source._baseFireRate;
            _baseProjectileCount = source._baseProjectileCount;
            _baseCriticalChance = source._baseCriticalChance;
            _baseCriticalDamage = source._baseCriticalDamage;
            _basePenetration = source._basePenetration;
            _baseTrackingSpeed = source._baseTrackingSpeed;
        }

        public void ResetToBase()
        {
            _damage = _baseDamage;
            _range = _baseRange;
            _fireRate = _baseFireRate;
            _projectileCount = _baseProjectileCount;
            _criticalChance = _baseCriticalChance;
            _criticalDamage = _baseCriticalDamage;
            _penetration = _basePenetration;
            _trackingSpeed = _baseTrackingSpeed;
            HasHoming = false;
            HomingStrength = 0f;
            HasAreaDamage = false;
            AreaDamageRadius = 0f;
            OnAttributesChanged?.Invoke();
        }

        private void SetDefaultBaseValues()
        {
            _baseDamage = DefaultDamage;
            _baseRange = DefaultRange;
            _baseFireRate = DefaultFireRate;
            _baseProjectileCount = DefaultProjectileCount;
            _baseCriticalChance = DefaultCriticalChance;
            _baseCriticalDamage = DefaultCriticalDamage;
            _basePenetration = DefaultPenetration;
            _baseTrackingSpeed = DefaultTrackingSpeed;
        }

        public string GetDescription()
        {
            StringBuilder description = new StringBuilder();
            description.AppendLine($"伤害: {Damage:F0}  射程: {Range:F1}  射速: {FireRate:F2}/s");
            description.AppendLine($"弹丸: {ProjectileCount}  暴击: {CriticalChance:P0}×{CriticalDamage:F1}");
            description.Append($"穿透: {Penetration}  追踪: {(HasHoming ? "是" : "否")}");
            description.AppendLine();
            description.Append($"跟踪: {TrackingSpeed:F1}");
            if (HasAreaDamage)
                description.Append($"  范围伤害: {AreaDamageRadius:F1}m");
            return description.ToString();
        }
    }
}
