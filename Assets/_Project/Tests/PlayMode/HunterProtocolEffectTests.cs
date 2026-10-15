using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _Project.Tests.PlayMode
{
    /// <summary>验证猎王协议伤害倍率会因重施放刷新，并在时限结束后恢复。</summary>
    public sealed class HunterProtocolEffectTests
    {
        private const float EffectDuration = 0.12f;
        private const float AttackDamageMultiplier = 2f;

        /// <summary>重施放会延长倍率有效期，且过期读取回到默认倍率。</summary>
        [UnityTest]
        public IEnumerator RecastRefreshesAttackMultiplierExpiry()
        {
            Type serviceType = Type.GetType(
                "CombatSystem.TransmitterGlobalEffectService, Assembly-CSharp",
                true);
            object service = Activator.CreateInstance(serviceType);
            MethodInfo setMultiplier = serviceType.GetMethod("SetAttackDamageMultiplier");
            PropertyInfo multiplier = serviceType.GetProperty("AttackDamageMultiplier");

            setMultiplier.Invoke(service, new object[] { AttackDamageMultiplier, 23, EffectDuration });
            yield return new WaitForSeconds(EffectDuration * 0.65f);
            setMultiplier.Invoke(service, new object[] { AttackDamageMultiplier, 23, EffectDuration });
            yield return new WaitForSeconds(EffectDuration * 0.65f);

            Assert.That(multiplier.GetValue(service), Is.EqualTo(AttackDamageMultiplier));

            yield return new WaitForSeconds(EffectDuration * 0.5f);

            Assert.That(multiplier.GetValue(service), Is.EqualTo(1f));
        }
    }
}
