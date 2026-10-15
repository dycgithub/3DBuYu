using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

/// <summary>验证猎王协议只增强发射器攻击，并在效果清理或到期后恢复伤害。</summary>
public sealed class HunterProtocolTests
{
    private ProjectileProfileSO _profile;
    private TransmitterGlobalEffectService _globalEffects;
    private GameObject _targetObject;

    /// <summary>创建攻击测试所需的配置和全局状态。</summary>
    [SetUp]
    public void SetUp()
    {
        _profile = ScriptableObject.CreateInstance<ProjectileProfileSO>();
        _globalEffects = new TransmitterGlobalEffectService();
    }

    /// <summary>释放临时投射物配置。</summary>
    [TearDown]
    public void TearDown()
    {
        if (_profile != null)
        {
            Object.DestroyImmediate(_profile);
        }

        if (_targetObject != null)
        {
            Object.DestroyImmediate(_targetObject);
        }
    }

    /// <summary>协议生效时，发射器的最终攻击伤害翻倍。</summary>
    [Test]
    public void FocusEffectDoublesEmitterAttackDamage()
    {
        _targetObject = new GameObject("HunterProtocolTestTarget");
        _targetObject.SetActive(false);
        Enemy target = _targetObject.AddComponent<Enemy>();
        var centralEffects = new CentralSkillEffectService(null, _globalEffects);
        Assert.That(
            centralEffects.TryFocusHighestHealth(new IDamageable[] { target }, 5f, 23),
            Is.True);

        AttackData attack = BuildEmitterAttack();

        Assert.That(attack.DamageAmount, Is.EqualTo(24f));
    }

    /// <summary>战斗效果清理后，发射器伤害恢复为装备计算结果。</summary>
    [Test]
    public void ClearAllRestoresEmitterAttackDamage()
    {
        _globalEffects.SetAttackDamageMultiplier(2f, 23, 5f);
        _globalEffects.ClearAll();

        AttackData attack = BuildEmitterAttack();

        Assert.That(attack.DamageAmount, Is.EqualTo(12f));
    }

    /// <summary>按来源清理只移除对应物品提交的伤害倍率。</summary>
    [Test]
    public void ClearOtherSourcePreservesEmitterAttackDamage()
    {
        _globalEffects.SetAttackDamageMultiplier(2f, 23, 5f);
        _globalEffects.ClearSource(24);

        AttackData attack = BuildEmitterAttack();

        Assert.That(attack.DamageAmount, Is.EqualTo(24f));
    }

    /// <summary>移除协议来源后，发射器伤害恢复为默认值。</summary>
    [Test]
    public void ClearMatchingSourceRestoresEmitterAttackDamage()
    {
        _globalEffects.SetAttackDamageMultiplier(2f, 23, 5f);
        _globalEffects.ClearSource(23);

        AttackData attack = BuildEmitterAttack();

        Assert.That(attack.DamageAmount, Is.EqualTo(12f));
    }

    /// <summary>通过发射器攻击构建入口取得实际最终伤害。</summary>
    /// <returns>发射器攻击数据。</returns>
    private AttackData BuildEmitterAttack()
    {
        var input = new TransmitterAttackInput(
            1,
            0,
            _profile,
            Vector3.zero,
            Vector3.forward,
            12f,
            60f,
            2f,
            1,
            0,
            0f,
            2f);
        var buildService = new TransmitterAttackBuildService(null, _globalEffects);
        TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
        var attackBuilder = new AttackDataBuilder();

        Assert.That(attackBuilder.TryBuildAttackData(in input, in build, out AttackData attack), Is.True);
        return attack;
    }
}
