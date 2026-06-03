using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TokenFormatTests
    {
        [Test] public void Abbr_Zero_ReturnsZero()     => Assert.AreEqual("0",      TokenFormat.Abbr(0));
        [Test] public void Abbr_999_ReturnsAsIs()      => Assert.AreEqual("999",    TokenFormat.Abbr(999));
        [Test] public void Abbr_1000_ReturnsOneK()     => Assert.AreEqual("1.0k",   TokenFormat.Abbr(1000));
        [Test] public void Abbr_1234_Returns1p2k()     => Assert.AreEqual("1.2k",   TokenFormat.Abbr(1234));
        [Test] public void Abbr_12345_Returns12p3k()   => Assert.AreEqual("12.3k",  TokenFormat.Abbr(12345));
        [Test] public void Abbr_Negative_ReturnsAsIs() => Assert.AreEqual("-5",     TokenFormat.Abbr(-5));
    }
}
