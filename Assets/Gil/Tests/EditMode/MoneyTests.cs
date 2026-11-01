using Gil.Core.Items;
using NUnit.Framework;

namespace Gil.Tests
{
    public class MoneyTests
    {
        [TestCase(0, "0푼")]
        [TestCase(7, "7푼")]
        [TestCase(35, "3전 5푼")]
        [TestCase(50, "5전")]
        [TestCase(100, "1냥")]
        [TestCase(352, "3냥 5전 2푼")]
        [TestCase(1205, "12냥 5푼")]
        [TestCase(-150, "-1냥 5전")]
        public void FormatsNyangJeonPun(int pun, string expected)
        {
            Assert.That(Money.Format(pun), Is.EqualTo(expected));
        }
    }
}
