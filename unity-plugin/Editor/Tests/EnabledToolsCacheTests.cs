// TDD tests for #29: enabled-tools cache is always warm (never computed off-thread).
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class EnabledToolsCacheTests
    {
        // After RegisterAll (called by CommandRegistry static ctor, triggered on any registry access),
        // the cache must already be populated — PeekEnabledToolsCache must be non-null.
        [Test]
        public void Cache_IsWarm_AfterRegistration()
        {
            // Touching CommandRegistry triggers its static ctor → RegisterAll → eager populate.
            _ = CommandRegistry.GetAllCommands();
            Assert.IsNotNull(CommandRouter.PeekEnabledToolsCache,
                "Cache must be non-null after RegisterAll");
        }

        // InvalidateEnabledToolsCache now REPOPULATES — cache must remain non-null after the call.
        [Test]
        public void InvalidateEnabledToolsCache_RepopulatesImmediately()
        {
            // Ensure registration has happened
            _ = CommandRegistry.GetAllCommands();

            CommandRouter.InvalidateEnabledToolsCache();

            Assert.IsNotNull(CommandRouter.PeekEnabledToolsCache,
                "Cache must be non-null after InvalidateEnabledToolsCache (repopulate, not null)");
        }

        // EnsureEnabledToolsCacheWarm triggers RegisterAll; cache must be non-null after the call.
        [Test]
        public void EnsureEnabledToolsCacheWarm_LeavesCacheNonNull()
        {
            CommandRouter.EnsureEnabledToolsCacheWarm();
            Assert.IsNotNull(CommandRouter.PeekEnabledToolsCache,
                "Cache must be non-null after EnsureEnabledToolsCacheWarm");
        }

        // ExecGetEnabledToolsCached must never return null — the ?? "" fallback guarantees it.
        [Test]
        public void ExecGetEnabledToolsCached_NeverReturnsNull()
        {
            _ = CommandRegistry.GetAllCommands();
            var result = CommandRouter.ExecGetEnabledToolsCached();
            Assert.IsNotNull(result, "ExecGetEnabledToolsCached must never return null");
        }
    }
}
