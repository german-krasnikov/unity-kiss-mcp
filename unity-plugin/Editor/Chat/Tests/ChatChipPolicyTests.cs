// NUnit tests for ChatChipPolicy type-allowlist.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatChipPolicyTests
    {
        [Test] public void Prefab_Allowed()     => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.GameObject)));
        [Test] public void Material_Allowed()   => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Material)));
        [Test] public void Texture2D_Allowed()  => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Texture2D)));
        [Test] public void Texture_Allowed()    => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Texture)));
        [Test] public void AnimClip_Allowed()   => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.AnimationClip)));
        [Test] public void MonoScript_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEditor.MonoScript)));
        [Test] public void ScriptableObject_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.ScriptableObject)));

        [Test] public void Cubemap_Allowed()       => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Cubemap)));
        [Test] public void RenderTexture_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.RenderTexture)));
        [Test] public void UserScriptableObjectSubclass_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(FakeSO)));

        [Test] public void DefaultAsset_Rejected() => Assert.IsFalse(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEditor.DefaultAsset)));
        [Test] public void Null_Rejected()          => Assert.IsFalse(ChatChipPolicy.IsAllowedAssetType(null));
        [Test] public void AudioClip_Rejected()     => Assert.IsFalse(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.AudioClip)));

        private class FakeSO : UnityEngine.ScriptableObject {}
    }
}
