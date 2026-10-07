using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AfterHours.Tests
{
    /// <summary>The label under the reticle while rubbish is held says which bin takes it, in words.</summary>
    public class BinTests
    {
        static string Plain(string rich) => Regex.Replace(rich, "<[^>]+>", "");

        [Test]
        public void HeldRubbishSaysWhichBin()
        {
            Assert.AreEqual("SODA CAN  ·  BLUE RECYCLING", Plain(Bin.HeldLabel("Soda can", TrashKind.Recyclable, null)));
            Assert.AreEqual("BANANA PEEL  ·  BLACK BIN", Plain(Bin.HeldLabel("Banana peel", TrashKind.General, null)));
            Assert.AreEqual("PAPER BALL  ·  EITHER BIN", Plain(Bin.HeldLabel("Paper ball", TrashKind.Paper, null)));
        }

        [Test]
        public void ABinUnderTheReticleSaysWhetherItTakesIt()
        {
            foreach (TrashKind k in System.Enum.GetValues(typeof(TrashKind)))
                foreach (Bin.BinKind b in System.Enum.GetValues(typeof(Bin.BinKind)))
                {
                    string label = Plain(Bin.HeldLabel("Thing", k, b));
                    StringAssert.StartsWith(b == Bin.BinKind.Recycle ? "BLUE RECYCLING" : "BLACK BIN", label);
                    // The label agrees with what the bin does when the item lands in it.
                    Assert.AreEqual(Bin.Accepts(b, k), label.Contains("✓"), $"{k} in {b}: {label}");
                    Assert.AreEqual(!Bin.Accepts(b, k), label.Contains("✗"), $"{k} in {b}: {label}");
                }
            Assert.AreEqual("BLACK BIN  ✗  ·  THIS ONE GOES IN BLUE", Plain(Bin.HeldLabel("Soda can", TrashKind.Recyclable, Bin.BinKind.Trash)));
            Assert.AreEqual("BLUE RECYCLING  ✗  ·  THIS ONE GOES IN BLACK", Plain(Bin.HeldLabel("Banana peel", TrashKind.General, Bin.BinKind.Recycle)));
            Assert.IsTrue(Bin.Accepts(Bin.BinKind.Trash, TrashKind.Paper) && Bin.Accepts(Bin.BinKind.Recycle, TrashKind.Paper), "paper goes in either");
        }
    }
}
