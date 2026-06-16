using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class AssetDatabaseHelper
    {
        const int MaxFindResults = 200;
        static readonly string[] ValidActions = { "find", "get_info", "create", "move", "validate_move", "duplicate", "delete", "get_dependencies", "import_settings", "export_package", "import_package" };

        internal static string Execute(string action, string argsJson)
        {
            switch (action)
            {
                case "find":            return Find(argsJson);
                case "get_info":        return GetInfo(argsJson);
                case "create":          return Create(argsJson);
                case "move":            return Move(argsJson);
                case "validate_move":   return ValidateMove(argsJson);
                case "duplicate":       return Duplicate(argsJson);
                case "delete":          return Delete(argsJson);
                case "get_dependencies":return GetDependencies(argsJson);
                case "import_settings": return ImportSettings(argsJson);
                case "export_package": return ExportPackage(argsJson);
                case "import_package": return ImportPackage(argsJson);
                default:                throw new System.Exception(ErrorHelper.InvalidAction(action, ValidActions));
            }
        }

        static string Find(string argsJson)
        {
            var type    = JsonHelper.ExtractString(argsJson, "type");
            var name    = JsonHelper.ExtractString(argsJson, "name");
            var folder  = JsonHelper.ExtractString(argsJson, "folder");
            var labels  = JsonHelper.ExtractString(argsJson, "labels");

            var filter = new StringBuilder();
            if (!string.IsNullOrEmpty(type))   filter.Append("t:").Append(type);
            if (!string.IsNullOrEmpty(name))   { if (filter.Length > 0) filter.Append(' '); filter.Append(name); }
            if (!string.IsNullOrEmpty(labels))
            {
                foreach (var lbl in labels.Split(','))
                {
                    var l = lbl.Trim();
                    if (l.Length > 0) { if (filter.Length > 0) filter.Append(' '); filter.Append("l:").Append(l); }
                }
            }

            if (filter.Length == 0 && string.IsNullOrEmpty(folder))
                throw new System.Exception("At least one of type/name/folder/labels is required for find");

            var searchFolders = string.IsNullOrEmpty(folder) ? null : new[] { folder };
            var guids = AssetDatabase.FindAssets(filter.ToString(), searchFolders);

            var sb = new StringBuilder();
            int count = 0;
            foreach (var guid in guids)
            {
                if (count >= MaxFindResults) { sb.Append($"\n({guids.Length - MaxFindResults} more...)"); break; }
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(AssetDatabase.GUIDToAssetPath(guid));
                count++;
            }
            return sb.Length == 0 ? "(no results)" : sb.ToString();
        }

        static string GetInfo(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");

            var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (mainType == null) throw new System.Exception($"Asset not found: {path}");

            var guid = AssetDatabase.AssetPathToGUID(path);
            var fullPath = Path.GetFullPath(path);
            var size = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
            var deps = AssetDatabase.GetDependencies(path, false);

            var sb = new StringBuilder();
            sb.Append("type: ").Append(mainType.Name).Append('\n');
            sb.Append("guid: ").Append(guid).Append('\n');
            sb.Append("size: ").Append(size).Append('\n');
            sb.Append("dependencies: ").Append(deps.Length).Append('\n');
            foreach (var d in deps) sb.Append(d).Append('\n');
            return sb.ToString().TrimEnd();
        }

        static void ValidatePath(string path)
        {
            if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/"))
                throw new System.ArgumentException($"Path must start with Assets/ or Packages/: {path}");
        }

        static string Create(string argsJson)
        {
            var type = JsonHelper.ExtractString(argsJson, "type");
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");
            if (string.IsNullOrEmpty(type)) throw new System.Exception("type is required");
            ValidatePath(path);

            if (type == "Folder")
            {
                var parent = Path.GetDirectoryName(path).Replace('\\', '/');
                var folderName = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder(parent, folderName);
                return "ok: " + path;
            }

            AssetHelper.EnsureDirectory(path);

            Object asset;
            switch (type)
            {
                case "Material":
                    var shader = Shader.Find("Standard")
                        ?? Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("HDRP/Lit")
                        ?? Shader.Find("Hidden/InternalErrorShader");
                    if (shader == null)
                        throw new System.Exception("No default shader found. Specify shader via 'shader' arg.");
                    asset = new Material(shader);
                    break;
                case "PhysicMaterial":
                    asset = new PhysicsMaterial();
                    break;
                default:
                    throw new System.Exception($"Unsupported create type '{type}'. Valid: Folder|Material|PhysicMaterial");
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return "ok: " + path;
        }

        static string Move(string argsJson)
        {
            var source = JsonHelper.ExtractString(argsJson, "source");
            var dest   = JsonHelper.ExtractString(argsJson, "dest");
            if (string.IsNullOrEmpty(source)) throw new System.Exception("source is required");
            if (string.IsNullOrEmpty(dest))   throw new System.Exception("dest is required");
            ValidatePath(source);
            ValidatePath(dest);

            var preCheck = AssetDatabase.ValidateMoveAsset(source, dest);
            if (!string.IsNullOrEmpty(preCheck)) throw new System.Exception(preCheck);

            var error = AssetDatabase.MoveAsset(source, dest);
            if (!string.IsNullOrEmpty(error)) throw new System.Exception(error);
            return "ok: " + dest;
        }

        static string ValidateMove(string argsJson)
        {
            var source = JsonHelper.ExtractString(argsJson, "source");
            var dest   = JsonHelper.ExtractString(argsJson, "dest");
            if (string.IsNullOrEmpty(source)) throw new System.Exception("source is required");
            if (string.IsNullOrEmpty(dest))   throw new System.Exception("dest is required");
            ValidatePath(source);
            ValidatePath(dest);
            var error = AssetDatabase.ValidateMoveAsset(source, dest);
            if (!string.IsNullOrEmpty(error))
                throw new System.Exception(error);
            return "ok";
        }

        static string Duplicate(string argsJson)
        {
            var source = JsonHelper.ExtractString(argsJson, "source");
            var dest   = JsonHelper.ExtractString(argsJson, "dest");
            if (string.IsNullOrEmpty(source)) throw new System.Exception("source is required");
            if (string.IsNullOrEmpty(dest))   throw new System.Exception("dest is required");
            ValidatePath(source);
            ValidatePath(dest);

            if (!AssetDatabase.CopyAsset(source, dest))
                throw new System.Exception($"CopyAsset failed: {source} → {dest}");
            return "ok";
        }

        static string Delete(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");
            ValidatePath(path);

            if (!AssetDatabase.DeleteAsset(path))
                throw new System.Exception($"DeleteAsset failed: {path}");
            return "ok";
        }

        static string GetDependencies(string argsJson)
        {
            var path      = JsonHelper.ExtractString(argsJson, "path");
            var recursive = JsonHelper.ExtractString(argsJson, "recursive");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");

            bool recurse = recursive == "true" || recursive == "True" || recursive == "1";
            var deps = AssetDatabase.GetDependencies(path, recurse);

            var sb = new StringBuilder();
            foreach (var d in deps) { if (sb.Length > 0) sb.Append('\n'); sb.Append(d); }
            return sb.Length == 0 ? "(no dependencies)" : sb.ToString();
        }

        static string ImportSettings(string argsJson)
        {
            var path  = JsonHelper.ExtractString(argsJson, "path");
            var prop  = JsonHelper.ExtractString(argsJson, "prop");
            var value = JsonHelper.ExtractString(argsJson, "value");
            if (string.IsNullOrEmpty(path))  throw new System.Exception("path is required");
            if (string.IsNullOrEmpty(prop))  throw new System.Exception("prop is required");
            if (string.IsNullOrEmpty(value)) throw new System.Exception("value is required");

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.Exception($"No importer found for: {path}");

            var propInfo = importer.GetType().GetProperty(prop,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (propInfo == null)
                throw new System.Exception($"Property '{prop}' not found on {importer.GetType().Name}");
            if (!propInfo.CanWrite)
                throw new System.Exception($"Property '{prop}' on {importer.GetType().Name} is read-only");

            object parsed;
            var t = propInfo.PropertyType;
            if (t == typeof(bool))
                parsed = value == "true" || value == "1";
            else if (t == typeof(int))
            {
                if (!int.TryParse(value, out int iv))
                    throw new System.Exception($"Cannot parse '{value}' as int for '{prop}'");
                parsed = iv;
            }
            else if (t == typeof(float))
            {
                if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv))
                    throw new System.Exception($"Cannot parse '{value}' as float for '{prop}'");
                parsed = fv;
            }
            else if (t.IsEnum)
            {
                try { parsed = System.Enum.Parse(t, value, ignoreCase: true); }
                catch { throw new System.Exception($"Cannot parse '{value}' as {t.Name}. Valid values: {string.Join(", ", System.Enum.GetNames(t))}"); }
            }
            else
                parsed = value;

            propInfo.SetValue(importer, parsed);
            importer.SaveAndReimport();
            return "ok";
        }

        static string ExportPackage(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var output = JsonHelper.ExtractString(argsJson, "output");
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(output))
                throw new System.Exception("export_package requires 'path' and 'output'");
            var includeDeps = JsonHelper.ExtractString(argsJson, "include_deps") != "false";
            var opts = ExportPackageOptions.Recurse;
            if (includeDeps) opts |= ExportPackageOptions.IncludeDependencies;
            AssetDatabase.ExportPackage(path, output, opts);
            return $"Exported to {output}";
        }

        static string ImportPackage(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path))
                throw new System.Exception("import_package requires 'path'");
            if (!System.IO.File.Exists(path))
                throw new System.Exception($"Package not found: {path}");
            AssetDatabase.ImportPackage(path, false);
            return "ok";
        }
    }
}
