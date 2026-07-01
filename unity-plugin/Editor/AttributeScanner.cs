using System;
using System.Linq;
using System.Reflection;

namespace UnityMCP.Editor
{
    internal static class AttributeScanner
    {
        internal static int ScanAndRegister()
        {
            int count = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!IsUserAssembly(asm)) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                foreach (var type in types)
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        var attr = method.GetCustomAttribute<MCPToolAttribute>();
                        if (attr == null) continue;
                        ValidateSignature(method);
                        var m = method;
                        CommandRegistry.Register(attr.Name,
                            args => (string)m.Invoke(null, new object[] { args }),
                            attr.Mutating, attr.Runtime,
                            attr.Required ?? "", attr.Optional ?? "");
                        count++;
                    }
            }
            return count;
        }

        private static bool IsUserAssembly(Assembly asm)
        {
            var name = asm.GetName().Name;
            if (name.StartsWith("Unity") || name.StartsWith("UnityMCP") ||
                name.StartsWith("System") || name.StartsWith("mscorlib") ||
                name.StartsWith("netstandard") || name.StartsWith("Mono") ||
                name.StartsWith("nunit") || name.StartsWith("Microsoft"))
                return false;
            return true;
        }

        private static void ValidateSignature(MethodInfo method)
        {
            var pars = method.GetParameters();
            if (method.ReturnType != typeof(string) || pars.Length != 1 || pars[0].ParameterType != typeof(string))
                throw new InvalidOperationException(
                    $"[MCPTool] method {method.DeclaringType.Name}.{method.Name} must be: static string Method(string args)");
        }
    }
}
