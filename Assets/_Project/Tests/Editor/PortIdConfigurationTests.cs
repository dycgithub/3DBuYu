using System.Collections.Generic;
using NUnit.Framework;
using Play;
using UnityEditor;

namespace _Project.Tests.Editor
{
    public sealed class PortIdConfigurationTests
    {
        private const string TransmitterFolder = "Assets/_Project/SO/Transmitter";

        [Test]
        public void EveryTransmitter_HasAUniqueStablePortId()
        {
            string[] guids = AssetDatabase.FindAssets("t:TransmitterSO", new[] { TransmitterFolder });
            var portIds = new HashSet<string>();

            Assert.That(guids, Has.Length.EqualTo(8));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TransmitterSO transmitter = AssetDatabase.LoadAssetAtPath<TransmitterSO>(path);

                Assert.That(transmitter, Is.Not.Null, path);
                Assert.That(transmitter.portId, Is.Not.Null.And.Not.Empty, path);
                Assert.That(portIds.Add(transmitter.portId), Is.True, path);
            }
        }
    }
}
