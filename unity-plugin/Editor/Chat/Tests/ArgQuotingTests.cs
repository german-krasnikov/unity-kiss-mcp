using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ArgQuotingTests
    {
        [Test] public void Simple_Unchanged()          => Assert.AreEqual("code-reviewer",        ArgQuoting.Quote("code-reviewer"));
        [Test] public void Plain_Unchanged()            => Assert.AreEqual("plain",                ArgQuoting.Quote("plain"));
        [Test] public void Empty_QuotedEmpty()          => Assert.AreEqual("\"\"",                 ArgQuoting.Quote(""));
        [Test] public void Null_QuotedEmpty()           => Assert.AreEqual("\"\"",                 ArgQuoting.Quote(null));
        [Test] public void Space_Wrapped()              => Assert.AreEqual("\"a b\"",              ArgQuoting.Quote("a b"));
        [Test] public void PathWithSpace_Wrapped()      => Assert.AreEqual("\"/Users/John Doe/x\"", ArgQuoting.Quote("/Users/John Doe/x"));
        [Test] public void EmbeddedQuotes_Escaped()     => Assert.AreEqual("\"say \\\"hi\\\"\"",   ArgQuoting.Quote("say \"hi\""));
        [Test] public void Backslash_EscapedAndWrapped() => Assert.AreEqual("\"a\\\\b\"",          ArgQuoting.Quote("a\\b"));
        [Test] public void Dollar_WrappedNotEscaped()   => Assert.AreEqual("\"$HOME\"",            ArgQuoting.Quote("$HOME"));
    }
}
