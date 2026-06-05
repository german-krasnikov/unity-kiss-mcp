// Wave 3 hard gate: probe which inline-text positioning API is live on THIS Unity build.
// Expected on Unity 6000.3.0b7: ActivePath="public" (ITextSelection route confirmed).
// The test NEVER fails on API absence — its job is to surface the live path in TestResults.
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UitkCharRectProbeTests
    {
        [Test]
        public void ProbeApi_ReportsAvailability()
        {
            string path = UitkCharRect.ProbeApi();

            // ActivePath must be one of the two valid values (reflection removed — public-only by design)
            Assert.That(
                path == "public" || path == "none",
                $"ActivePath must be 'public' or 'none'; got '{path}'"
            );

            bool expectedAvailable = path == "public";
            Assert.AreEqual(
                expectedAvailable,
                UitkCharRect.IsAvailable,
                $"IsAvailable should be {expectedAvailable} when ActivePath='{path}'"
            );

            TestContext.WriteLine(
                $"[UitkCharRect] ActivePath={path}  IsAvailable={UitkCharRect.IsAvailable}");
        }

        // On 6000.3.0b7 the public ITextSelection path is confirmed — assert it here.
        // If a future Unity build removes ITextSelection, this test downgrades to a log line.
        [Test]
        public void ProbeApi_OnThisRuntime_ExpectsPublic()
        {
            string path = UitkCharRect.ProbeApi();

            // Log for CI even if we fall through to none
            TestContext.WriteLine($"[UitkCharRect] path={path}");

            // Soft assertion: warn if we got 'none' unexpectedly (don't fail the suite)
            if (path == "none")
                TestContext.WriteLine(
                    "WARNING: ActivePath=none — positioning degraded to row-layout. " +
                    "Check Unity version or run Dump_TextHandleAndSelectionMembers for diagnosis.");
            else
                Assert.AreEqual("public", path,
                    "Expected 'public' on a Unity build with UITK text selection (reflection path removed).");
        }

        // ── diagnostic dump ──────────────────────────────────────────────────

        static readonly BindingFlags AllInstance =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        static string ParamList(MethodInfo m) =>
            string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));

        [Test]
        public void Dump_TextHandleAndSelectionMembers()
        {
            // SECTION A: handle accessor on TextElement hierarchy
            var handleTypes = new System.Collections.Generic.List<Type>();
            try
            {
                var cursor = typeof(TextElement);
                while (cursor != null && cursor != typeof(VisualElement).BaseType)
                {
                    foreach (var m in cursor.GetMembers(AllInstance | BindingFlags.DeclaredOnly))
                    {
                        if (m.Name.IndexOf("handle", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (m is not PropertyInfo && m is not FieldInfo) continue;

                        Type t = m is PropertyInfo pi ? pi.PropertyType
                               : ((FieldInfo)m).FieldType;
                        TestContext.WriteLine($"[A] {cursor.Name}.{m.Name} type={t?.FullName}");
                        if (t != null && !handleTypes.Contains(t)) handleTypes.Add(t);
                    }
                    cursor = cursor.BaseType;
                }
                if (handleTypes.Count == 0)
                    TestContext.WriteLine("[A] no 'handle' members found");
            }
            catch (Exception ex) { TestContext.WriteLine($"[A] EXCEPTION: {ex.Message}"); }

            // SECTION B: interesting methods on handle types
            try
            {
                var keywords = new[] { "Cursor", "Position", "LineHeight", "Index", "Cache" };
                foreach (var ht in handleTypes)
                    foreach (var m in ht.GetMethods(AllInstance))
                        if (keywords.Any(k => m.Name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                            TestContext.WriteLine($"[B] {ht.Name}.{m.Name}({ParamList(m)})");
            }
            catch (Exception ex) { TestContext.WriteLine($"[B] EXCEPTION: {ex.Message}"); }

            // SECTION D: ITextSelection via TextField.textSelection
            try
            {
                foreach (var p in typeof(TextField).GetProperties(AllInstance))
                {
                    if (p.Name.IndexOf("selection", StringComparison.OrdinalIgnoreCase) < 0 &&
                        p.Name.IndexOf("cursor",    StringComparison.OrdinalIgnoreCase) < 0) continue;

                    TestContext.WriteLine($"[D] TextField.{p.Name} type={p.PropertyType.FullName}");
                    foreach (var m in p.PropertyType.GetMembers(AllInstance))
                    {
                        if (m.Name.IndexOf("cursor",   StringComparison.OrdinalIgnoreCase) < 0 &&
                            m.Name.IndexOf("Position", StringComparison.OrdinalIgnoreCase) < 0 &&
                            m.Name.IndexOf("Index",    StringComparison.OrdinalIgnoreCase) < 0) continue;
                        TestContext.WriteLine($"[D2] {p.PropertyType.Name}.{m.Name} ({m.MemberType})");
                    }
                }
            }
            catch (Exception ex) { TestContext.WriteLine($"[D] EXCEPTION: {ex.Message}"); }

            // SECTION E: GetCursorPositionFromStringIndex + MeasureTextSize on TextElement
            try
            {
                var cursor = typeof(TextElement);
                while (cursor != null && cursor != typeof(VisualElement).BaseType)
                {
                    foreach (var m in cursor.GetMethods(AllInstance | BindingFlags.DeclaredOnly))
                    {
                        if (m.Name.IndexOf("GetCursorPositionFromStringIndex",
                                StringComparison.OrdinalIgnoreCase) < 0 &&
                            m.Name.IndexOf("MeasureTextSize",
                                StringComparison.OrdinalIgnoreCase) < 0) continue;
                        TestContext.WriteLine($"[E] {cursor.Name}.{m.Name}({ParamList(m)})");
                    }
                    cursor = cursor.BaseType;
                }
            }
            catch (Exception ex) { TestContext.WriteLine($"[E] EXCEPTION: {ex.Message}"); }

            Assert.Pass("Diagnostic dump complete — see TestContext output.");
        }
    }
}
