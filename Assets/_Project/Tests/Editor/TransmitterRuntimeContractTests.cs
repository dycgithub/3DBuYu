using NUnit.Framework;
using Play;
using UnityEngine;

namespace Play.Tests
{
    /// <summary>
    /// 验证端口运行时对象对无效配置和锁定模型的处理。
    /// </summary>
    public sealed class TransmitterRuntimeContractTests
    {
        [Test]
        public void NullPortConfiguration_IsSkippedWithoutBreakingValidPortIndexing()
        {
            CentralSO central = ScriptableObject.CreateInstance<CentralSO>();
            TransmitterSO validPort = ScriptableObject.CreateInstance<TransmitterSO>();
            validPort.portName = "valid";
            central.Transmitters = new[] { null, validPort };
            GameObject parentObject = new GameObject("central-parent");
            CentralTransmitterManager manager = null;

            try
            {
                manager = new CentralTransmitterManager(central, parentObject.transform, null);

                Assert.That(manager.PortCount, Is.EqualTo(1));
                Assert.That(manager.GetPort(0), Is.Not.Null);
                Assert.That(manager.GetPort(0).PortIndex, Is.EqualTo(0));
            }
            finally
            {
                manager?.Dispose();
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(validPort);
                Object.DestroyImmediate(central);
            }
        }

        [Test]
        public void LockedPortWithLockedModel_KeepsTheLockedModelVisible()
        {
            CentralSO central = ScriptableObject.CreateInstance<CentralSO>();
            TransmitterSO lockedPort = ScriptableObject.CreateInstance<TransmitterSO>();
            GameObject lockedModelPrefab = new GameObject("locked-model");
            lockedPort.portName = "locked";
            lockedPort.isInitiallyLocked = true;
            lockedPort.lockedModelPrefab = lockedModelPrefab;
            central.Transmitters = new[] { lockedPort };
            GameObject parentObject = new GameObject("central-parent");
            CentralTransmitterManager manager = null;

            try
            {
                manager = new CentralTransmitterManager(central, parentObject.transform, null);
                CentralTransmitterRuntime runtime = manager.GetPort(0);

                Assert.That(runtime.FirePoint.childCount, Is.EqualTo(1));
                Assert.That(runtime.FirePoint.GetChild(0).gameObject.activeSelf, Is.True);
            }
            finally
            {
                manager?.Dispose();
                Object.DestroyImmediate(lockedModelPrefab);
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(lockedPort);
                Object.DestroyImmediate(central);
            }
        }
    }
}
