using NUnit.Framework;
using Play;

/// <summary>Locks down the completed-state color permutation independently of the HUD.</summary>
public sealed class DiamondNetStateTests
{
    /// <summary>Every move and its inverse restores all fixed slots.</summary>
    [TestCase(0)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(6)]
    public void InverseRestoresColors(int move)
    {
        var state = new DiamondNetState();
        state.Apply((DiamondTwistMove)move);
        state.Apply((DiamondTwistMove)(move + 1));
        for (int face = 0; face < 8; face++)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                Assert.That(state.GetColor(face, slot), Is.EqualTo(face));
            }
        }
    }

    /// <summary>Three equal turns restore the solved identities and every step conserves four stickers per color.</summary>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public void TripleTurnConservesColors(int move)
    {
        var state = new DiamondNetState();
        for (int turn = 0; turn < 3; turn++)
        {
            state.Apply((DiamondTwistMove)move);
            int[] counts = new int[8];
            for (int face = 0; face < 8; face++)
            {
                for (int slot = 0; slot < 4; slot++)
                {
                    counts[state.GetColor(face, slot)]++;
                    if (turn == 2)
                    {
                        Assert.That(state.GetColor(face, slot), Is.EqualTo(face));
                    }
                }
            }

            CollectionAssert.AreEqual(new[] { 4, 4, 4, 4, 4, 4, 4, 4 }, counts);
        }
    }
}
