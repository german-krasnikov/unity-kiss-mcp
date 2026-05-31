using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class CodeExecutor
    {
        private static readonly string[] Blocked = {
            "System.Diagnostics.Process", "System.IO.File", "System.IO.Directory",
            "System.IO.Stream", "FileStream", "StreamWriter", "StreamReader",
            "System.IO.Path", "System.Net.", "WebClient", "HttpClient",
            "Assembly.Load", "AppDomain", "DllImport", "extern ", "unsafe ",
            "System.Reflection.Assembly", "Type.GetType", ".GetMethod(",
            ".Invoke(", "System.Threading", "System.Runtime.InteropServices",
            "Environment.GetEnvironmentVariable",
            "System.Reflection.Emit", "DynamicMethod", "ILGenerator", "OpCodes",
            "Activator", "System.Linq.Expressions.Expression",
            "GetMethods(", "CreateDelegate", "GetTypes(", "GetMembers(",
            "GetProperties(", "GetFields(", "GetConstructors(", ".Assembly",
        };

        private const string Usings =
            "using UnityEngine; using UnityEditor; using System; using System.Linq; using System.Collections.Generic;";

        // Lazy-loaded Roslyn via reflection
        private static Assembly _roslynCompiler;
        private static Assembly _roslynCore;
        private static bool _roslynLoaded;
        private static int _compilationCount;

        public static string Execute(string code, string undoLabel)
        {
            SecurityScan(code);

            var wrapped = WrapIfBareCode(code);

            EnsureRoslyn();

            var assembly = Compile(wrapped);
            return RunWithUndo(assembly, undoLabel);
        }

        // Exposed for tests
        internal static string WrapIfBareCode(string code)
        {
            if (code.Contains("class ") || code.Contains("namespace "))
                return code;

            return $"{Usings}\n" +
                   $"public static class __MCPScript {{ public static object Run() {{\n{code}\n}} }}";
        }

        private static void SecurityScan(string code)
        {
            foreach (var blocked in Blocked)
            {
                if (code.Contains(blocked))
                    throw new InvalidOperationException(
                        $"Security: blocked pattern '{blocked}'. Only UnityEngine/UnityEditor APIs allowed.");
            }
        }

        private static void EnsureRoslyn()
        {
            if (_roslynLoaded) return;

            var base_ = EditorApplication.applicationContentsPath;
            // Ordered by preference — Mono 32-bit compatible (PE32) paths first.
            // DotNetSdkRoslyn ships PE32+ (.NET 5+) which Mono runtime can't load.
            var candidates = new[] {
                Path.Combine(base_, "Resources", "Scripting", "MonoBleedingEdge", "lib", "mono", "msbuild", "Current", "bin", "Roslyn"),
                Path.Combine(base_, "Resources", "Scripting", "DotNetSdkRoslyn"),
                Path.Combine(base_, "DotNetSdkRoslyn"),
            };

            var roslynDir = candidates.FirstOrDefault(Directory.Exists);
            if (roslynDir == null)
                throw new InvalidOperationException(
                    $"Roslyn DLLs not found. Searched:\n  {string.Join("\n  ", candidates)}");

            _roslynCore = LoadAssembly(roslynDir, "Microsoft.CodeAnalysis.dll");
            _roslynCompiler = LoadAssembly(roslynDir, "Microsoft.CodeAnalysis.CSharp.dll");
            _roslynLoaded = true;
        }

        private static Assembly LoadAssembly(string dir, string name)
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path))
                throw new InvalidOperationException($"Roslyn DLL not found: {path}");
            return Assembly.LoadFrom(path);
        }

        private static Assembly Compile(string code)
        {
            if (_compilationCount >= 200)
                Debug.LogWarning("[MCP] execute_code: 200+ compilations — assembly leak risk in Mono. Consider restarting Unity.");
            _compilationCount++;

            // ParseText: find overload where first param is string (or SourceText) — use the one
            // that can accept only a string argument by filling remaining optional params with defaults.
            var syntaxTreeType = _roslynCompiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree");
            // Find ParseText overload where first param is string (not SourceText)
            var parseMethod = syntaxTreeType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "ParseText"
                    && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(string))
                .OrderBy(m => m.GetParameters().Length) // prefer shortest match
                .FirstOrDefault();

            if (parseMethod == null)
                throw new InvalidOperationException("CSharpSyntaxTree.ParseText(string,...) not found");

            var syntaxTree = parseMethod.Invoke(null, BuildInvokeArgs(parseMethod, code))
                ?? throw new InvalidOperationException("ParseText returned null");

            var refList = BuildReferences();

            // CSharpCompilation.Create — pick overload with (string, IEnumerable<SyntaxTree>, IEnumerable<MetadataRef>, options)
            var compilationType = _roslynCompiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation");
            var createMethod = compilationType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Create")
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();

            if (createMethod == null)
                throw new InvalidOperationException("CSharpCompilation.Create not found");

            var syntaxTreesArray = Array.CreateInstance(syntaxTree.GetType(), 1);
            syntaxTreesArray.SetValue(syntaxTree, 0);

            // Build CSharpCompilationOptions with OutputKind.DynamicallyLinkedLibrary
            var options = BuildCompilationOptions();

            var createParams = createMethod.GetParameters();
            var createArgs = new object[createParams.Length];
            createArgs[0] = "MCPScript";
            if (createParams.Length > 1) createArgs[1] = syntaxTreesArray;
            if (createParams.Length > 2) createArgs[2] = refList;
            if (createParams.Length > 3) createArgs[3] = options;
            for (int i = 4; i < createParams.Length; i++)
                createArgs[i] = createParams[i].HasDefaultValue ? createParams[i].DefaultValue : null;

            var compilation = createMethod.Invoke(null, createArgs);
            if (compilation == null)
                throw new InvalidOperationException("CSharpCompilation.Create returned null");

            // Emit to memory stream — find overload where first param accepts Stream
            using var ms = new MemoryStream();
            var emitMethod = compilation.GetType().GetMethods()
                .Where(m => m.Name == "Emit" && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(Stream))
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();

            if (emitMethod == null)
                throw new InvalidOperationException("Compilation.Emit not found");

            var emitResult = emitMethod.Invoke(compilation, BuildEmitArgs(ms, emitMethod));

            CheckEmitResult(emitResult);

            return Assembly.Load(ms.ToArray());
        }

        private static object BuildCompilationOptions()
        {
            // OutputKind enum: DynamicallyLinkedLibrary = 2
            var outputKindType = _roslynCompiler.GetType("Microsoft.CodeAnalysis.OutputKind")
                ?? _roslynCore.GetType("Microsoft.CodeAnalysis.OutputKind");
            var outputKindDll = outputKindType != null
                ? Enum.ToObject(outputKindType, 2)  // DynamicallyLinkedLibrary = 2
                : null;

            var optionsType = _roslynCompiler.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions");
            // Find constructor that accepts OutputKind as first parameter
            var ctor = optionsType.GetConstructors()
                .Where(c => c.GetParameters().Length > 0
                    && c.GetParameters()[0].ParameterType == outputKindType)
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (ctor == null) return null; // fallback: no options
            return ctor.Invoke(BuildInvokeArgs(ctor, outputKindDll));
        }

        // Build invocation args: first param = firstArg, rest = defaults/null
        private static object[] BuildInvokeArgs(MethodBase method, object firstArg)
        {
            var p = method.GetParameters();
            var args = new object[p.Length];
            args[0] = firstArg;
            for (int i = 1; i < p.Length; i++)
                args[i] = p[i].HasDefaultValue ? p[i].DefaultValue : null;
            return args;
        }

        private static object[] BuildEmitArgs(MemoryStream ms, MethodInfo emitMethod)
        {
            var paramCount = emitMethod.GetParameters().Length;
            var args = new object[paramCount];
            args[0] = ms; // first param is always the peStream
            // rest default to null
            return args;
        }

        private static Array BuildReferences()
        {
            var metaRefType = _roslynCore.GetType("Microsoft.CodeAnalysis.MetadataReference");
            if (metaRefType == null)
                throw new InvalidOperationException("MetadataReference type not found in Roslyn core assembly");
            // Find CreateFromFile: pick the overload where first param is string, fewest params
            var createFromFileMethod = metaRefType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "CreateFromFile"
                    && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(string))
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();
            if (createFromFileMethod == null)
                throw new InvalidOperationException("MetadataReference.CreateFromFile(string,...) not found");

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => IsAllowedAssembly(a))
                .Select(a => {
                    try { return a.Location; }
                    catch { return null; }
                })
                .Where(loc => !string.IsNullOrEmpty(loc) && File.Exists(loc))
                .Distinct()
                .ToArray();

            var refList = Array.CreateInstance(metaRefType, assemblies.Length);
            for (int i = 0; i < assemblies.Length; i++)
                refList.SetValue(
                    createFromFileMethod.Invoke(null, BuildInvokeArgs(createFromFileMethod, assemblies[i])), i);

            return refList;
        }

        private static bool IsAllowedAssembly(Assembly a)
        {
            var name = a.GetName().Name;
            return name == "mscorlib" || name == "netstandard" ||
                   name == "System" || name == "System.Core" ||
                   name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor") ||
                   name == "Assembly-CSharp" || name == "Assembly-CSharp-Editor";
        }

        private static void CheckEmitResult(object emitResult)
        {
            var successProp = emitResult.GetType().GetProperty("Success");
            if ((bool)successProp.GetValue(emitResult)) return;

            var diagnosticsProp = emitResult.GetType().GetProperty("Diagnostics");
            var diagnostics = (System.Collections.IEnumerable)diagnosticsProp.GetValue(emitResult);
            var errors = diagnostics.Cast<object>()
                .Where(d => {
                    var severity = d.GetType().GetProperty("Severity")?.GetValue(d)?.ToString();
                    return severity == "Error";
                })
                .Select(d => d.ToString())
                .ToArray();
            throw new InvalidOperationException("Compile error:\n" + string.Join("\n", errors));
        }

        private static string RunWithUndo(Assembly assembly, string undoLabel)
        {
            var type = assembly.GetTypes().FirstOrDefault(t => t.Name == "__MCPScript")
                       ?? assembly.GetTypes().First();
            var method = type.GetMethod("Run",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoLabel);
            var groupId = Undo.GetCurrentGroup();
            if (method == null)
                throw new InvalidOperationException($"No public/private static Run() method found in {type.FullName}");
            try
            {
                var result = method.Invoke(null, null);
                return result?.ToString() ?? "null";
            }
            finally
            {
                Undo.CollapseUndoOperations(groupId);
            }
        }
    }
}
