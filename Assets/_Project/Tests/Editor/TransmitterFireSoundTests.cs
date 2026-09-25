using NUnit.Framework;
using Play;
using UnityEngine;

public sealed class TransmitterFireSoundTests
{
    [Test]
    public void RuntimeFireSoundOverrideIsScopedAndResettable()
    {
        AudioClip defaultFirst = AudioClip.Create("DefaultFirst", 8, 1, 8000, false);
        AudioClip defaultSecond = AudioClip.Create("DefaultSecond", 8, 1, 8000, false);
        AudioClip runtimeOverride = AudioClip.Create("RuntimeOverride", 8, 1, 8000, false);
        TransmitterSO first = CreateTransmitter("first", defaultFirst);
        TransmitterSO second = CreateTransmitter("second", defaultSecond);
        CentralSO central = ScriptableObject.CreateInstance<CentralSO>();
        central.Transmitters = new[] { first, second };
        GameObject parent = new("TransmitterFireSoundTest");
        CentralTransmitterManager manager = null;

        try
        {
            manager = new CentralTransmitterManager(central, parent.transform, null);

            Assert.That(manager.TrySetFireSound("first", runtimeOverride), Is.True);
            Assert.That(manager.GetPortById("first").FireSound, Is.SameAs(runtimeOverride));
            Assert.That(manager.GetPortById("second").FireSound, Is.SameAs(defaultSecond));
            Assert.That(first.fireSound, Is.SameAs(defaultFirst));

            Assert.That(manager.TrySetFireSound("first", null), Is.True);
            Assert.That(manager.GetPortById("first").FireSound, Is.SameAs(defaultFirst));
        }
        finally
        {
            manager?.Dispose();
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(central);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(defaultFirst);
            Object.DestroyImmediate(defaultSecond);
            Object.DestroyImmediate(runtimeOverride);
        }
    }

    [Test]
    public void UnknownTransmitterCannotChangeFireSound()
    {
        TransmitterSO transmitter = CreateTransmitter(
            "known",
            AudioClip.Create("Default", 8, 1, 8000, false));
        CentralSO central = ScriptableObject.CreateInstance<CentralSO>();
        central.Transmitters = new[] { transmitter };
        GameObject parent = new("TransmitterFireSoundTest");
        AudioClip replacement = AudioClip.Create("Replacement", 8, 1, 8000, false);
        CentralTransmitterManager manager = null;

        try
        {
            manager = new CentralTransmitterManager(central, parent.transform, null);

            Assert.That(manager.TrySetFireSound("missing", replacement), Is.False);
            Assert.That(manager.GetPortById("known").FireSound, Is.SameAs(transmitter.fireSound));
        }
        finally
        {
            manager?.Dispose();
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(central);
            Object.DestroyImmediate(transmitter.fireSound);
            Object.DestroyImmediate(transmitter);
            Object.DestroyImmediate(replacement);
        }
    }

    private static TransmitterSO CreateTransmitter(string portId, AudioClip fireSound)
    {
        TransmitterSO transmitter = ScriptableObject.CreateInstance<TransmitterSO>();
        transmitter.portId = portId;
        transmitter.fireSound = fireSound;
        return transmitter;
    }
}
