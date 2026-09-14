using System.Reflection;
using NUnit.Framework;
using Play;

namespace _Project.Tests.Editor
{
    public sealed class TransmitterBackpackBinderTests
    {
        [Test]
        public void CentralCore_DoesNotOwnTransmitterBackpackViews()
        {
            FieldInfo field = typeof(CentralCore).GetField(
                "transmitterBackpacks",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Null);
        }
    }
}
