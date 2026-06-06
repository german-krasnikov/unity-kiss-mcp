using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    public class CodeExecutorSecurityTests
    {
        // ── Blocked patterns ─────────────────────────────────────────────────

        [Test]
        public void SecurityScan_EnvironmentExit_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("Environment.Exit(0); return null;"));

        [Test]
        public void SecurityScan_UsingSystemDiagnostics_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "using System.Diagnostics;\nclass X { static void Run() { Process.Start(\"calc\"); } }"));

        [Test]
        public void SecurityScan_UsingSystemIO_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "using System.IO;\nclass X { static void Run() { File.Delete(\"x\"); } }"));

        // TestCase covers remaining 31 blocked patterns
        [TestCase("System.Diagnostics.Process.Start(\"calc\");")]
        [TestCase("System.IO.File.Delete(\"x\");")]
        [TestCase("System.IO.Directory.Delete(\"x\");")]
        [TestCase("System.IO.Stream s = null;")]
        [TestCase("FileStream fs = null;")]
        [TestCase("StreamWriter sw = null;")]
        [TestCase("StreamReader sr = null;")]
        [TestCase("System.IO.Path.Combine(\"a\",\"b\");")]
        [TestCase("System.Net.WebClient wc = null;")]
        [TestCase("WebClient wc = null;")]
        [TestCase("HttpClient hc = null;")]
        [TestCase("Assembly.Load(new byte[0]);")]
        [TestCase("AppDomain.CurrentDomain.GetAssemblies();")]
        [TestCase("[DllImport(\"lib\")] static extern void Foo();")]
        [TestCase("unsafe void Foo() {}")]
        [TestCase("System.Reflection.Assembly.LoadFrom(\"x\");")]
        [TestCase("Type.GetType(\"X\");")]
        [TestCase("typeof(Foo).GetMethod(\"Bar\");")]
        [TestCase("method.Invoke(null, null);")]
        [TestCase("System.Threading.Thread t = null;")]
        [TestCase("System.Runtime.InteropServices.Marshal.Copy(null,0,default,0);")]
        [TestCase("Environment.GetEnvironmentVariable(\"PATH\");")]
        [TestCase("System.Reflection.Emit.OpCodes.Nop.ToString();")]
        [TestCase("DynamicMethod dm = null;")]
        [TestCase("ILGenerator il = null;")]
        [TestCase("OpCodes.Nop.ToString();")]
        [TestCase("Activator.CreateInstance(typeof(object));")]
        [TestCase("System.Linq.Expressions.Expression.Constant(1);")]
        [TestCase("typeof(Foo).GetMethods();")]
        [TestCase("typeof(Foo).CreateDelegate(null,null);")]
        [TestCase("asm.GetTypes();")]
        [TestCase("typeof(Foo).GetMembers();")]
        [TestCase("typeof(Foo).GetProperties();")]
        [TestCase("typeof(Foo).GetFields();")]
        [TestCase("typeof(Foo).GetConstructors();")]
        [TestCase("var x = obj.Assembly;")]
        [TestCase("Environment.SetEnvironmentVariable(\"X\",\"Y\");")]
        [TestCase("using System.Net;\nclass X {}")]
        [TestCase("using System.Reflection;\nclass X {}")]
        public void SecurityScan_BlockedPattern_Throws(string code)
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(code),
                $"Expected blocked pattern to throw for: {code}");

        // ── Legit snippets ───────────────────────────────────────────────────

        [Test]
        public void SecurityScan_FindGameObject_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan("return GameObject.Find(\"Player\")?.name;"));

        [Test]
        public void SecurityScan_UnityEditorSelection_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan(
                    "return UnityEditor.Selection.activeGameObject?.name ?? \"none\";"));

        [Test]
        public void SecurityScan_PureLinq_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan(
                    "var list = new System.Collections.Generic.List<int>{1,2,3}; return list.Count;"));

        [Test]
        public void SecurityScan_EmptyString_DoesNotThrow()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(""));

        // ── IsAllowedAssembly ────────────────────────────────────────────────

        private static readonly MethodInfo _isAllowedAssemblyMethod =
            typeof(CodeExecutor).GetMethod("IsAllowedAssembly",
                BindingFlags.NonPublic | BindingFlags.Static);

        private bool CallIsAllowed(Assembly a) =>
            (bool)_isAllowedAssemblyMethod.Invoke(null, new object[] { a });

        [TestCase("mscorlib")]
        [TestCase("netstandard")]
        [TestCase("System")]
        [TestCase("System.Core")]
        [TestCase("UnityEngine")]
        [TestCase("UnityEngine.CoreModule")]
        [TestCase("UnityEditor")]
        [TestCase("UnityEditor.CoreModule")]
        public void IsAllowedAssembly_AllowedName_ReturnsTrue(string asmName)
        {
            Assert.IsNotNull(_isAllowedAssemblyMethod, "IsAllowedAssembly not found");
            var target = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == asmName);
            Assert.IsNotNull(target, $"Assembly '{asmName}' not loaded in test domain");
            Assert.IsTrue(CallIsAllowed(target), $"Expected '{asmName}' to be allowed");
        }

        [Test]
        public void IsAllowedAssembly_TestAssembly_ReturnsFalse()
        {
            Assert.IsNotNull(_isAllowedAssemblyMethod, "IsAllowedAssembly not found");
            // UnityMCP.Editor.Tests is not in the allowlist
            var testAsm = Assembly.GetExecutingAssembly();
            Assert.IsFalse(CallIsAllowed(testAsm),
                $"Expected '{testAsm.GetName().Name}' to NOT be allowed");
        }

        [Test]
        public void IsAllowedAssembly_UnityMCPEditorPlugin_ReturnsFalse()
        {
            Assert.IsNotNull(_isAllowedAssemblyMethod, "IsAllowedAssembly not found");
            // The plugin assembly itself (UnityMCP.Editor) is not in the allowlist
            var pluginAsm = typeof(CodeExecutor).Assembly;
            Assert.IsFalse(CallIsAllowed(pluginAsm),
                $"Expected '{pluginAsm.GetName().Name}' to NOT be allowed");
        }
    }
}
