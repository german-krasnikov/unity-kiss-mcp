using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    public abstract class ChipTestBase
    {
        [SetUp]
        public virtual void ChipSetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        [TearDown]
        public virtual void ChipTearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }
    }
}
