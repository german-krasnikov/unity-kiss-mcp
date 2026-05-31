using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class BatchHelper
    {
        // Depth counter, not a bool: a nested `batch` command must not reset the flag
        // (and fire Physics.Sync) while the outer batch is still running. Sync once, at depth 0.
        private static int _batchDepth;
        internal static bool InBatch => _batchDepth > 0;

        public static string Execute(string commandsText, string onError, int timeoutMs = 25000)
        {
            var commands = ParseLines(commandsText);
            var sb = new StringBuilder();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool stopped = false;
            int okCount = 0, errCount = 0, timeoutCount = 0;

            _batchDepth++;
            try
            {

            for (int i = 0; i < commands.Count; i++)
            {
                if (stopped)
                {
                    sb.AppendLine($"[{i}] skip");
                    continue;
                }

                if (sw.ElapsedMilliseconds > Math.Max(timeoutMs, 1000))
                {
                    sb.AppendLine($"[{i}] TIMEOUT: batch deadline reached after {sw.ElapsedMilliseconds / 1000.0:F1}s");
                    for (int j = i + 1; j < commands.Count; j++)
                        sb.AppendLine($"[{j}] skip");
                    timeoutCount = commands.Count - i;
                    break;
                }

                var (cmd, argsJson) = commands[i];

                // Async-only commands cannot run inside batch
                if (cmd == "wait_until" || cmd == "move_to" || cmd == "run_tests" || cmd == "test_step" || cmd == "run_playtest")
                {
                    sb.AppendLine($"[{i}] err: '{cmd}' requires async dispatch, not supported in batch");
                    errCount++;
                    if (onError == "stop") stopped = true;
                    continue;
                }

                // Play Mode guards
                if (EditorApplication.isPlaying && CommandRegistry.IsMutating(cmd))
                {
                    sb.AppendLine($"[{i}] BLOCKED: '{cmd}' is mutating, skipped in Play Mode");
                    errCount++;
                    if (onError == "stop") stopped = true;
                    continue;
                }
                if (!EditorApplication.isPlaying && CommandRegistry.IsRuntime(cmd))
                {
                    sb.AppendLine($"[{i}] BLOCKED: '{cmd}' is runtime-only, skipped outside Play Mode");
                    errCount++;
                    if (onError == "stop") stopped = true;
                    continue;
                }

                // Compile guard
                if (EditorApplication.isCompiling && CommandRegistry.IsMutating(cmd)
                    && !CommandRouter.IsAllowedDuringCompile(cmd))
                {
                    sb.AppendLine($"[{i}] BLOCKED: '{cmd}' skipped during compilation");
                    errCount++;
                    if (onError == "stop") stopped = true;
                    continue;
                }

                // Tool enabled check
                if (!CommandRouter.IsAlwaysAllowed(cmd) && !MCPSettings.IsToolEnabled(cmd))
                {
                    sb.AppendLine($"[{i}] err: Tool '{cmd}' disabled");
                    errCount++;
                    if (onError == "stop") stopped = true;
                    continue;
                }

                // Validate schema before execution
                var validationErr = CommandSchema.Validate(cmd, argsJson);
                if (validationErr != null)
                {
                    sb.AppendLine($"[{i}] err: {validationErr}");
                    errCount++;
                    if (onError == "stop") stopped = true;
                    continue;
                }

                try
                {
                    var result = CommandRouter.ExecuteCommand(cmd, argsJson);
                    if (result != "ok")
                        sb.AppendLine($"[{i}] {result}");
                    okCount++;
                }
                catch (Exception e)
                {
                    sb.AppendLine($"[{i}] err: {e.Message}");
                    errCount++;
                    if (onError == "stop") stopped = true;
                }
            }

            // Summary line
            var summary = errCount > 0 || timeoutCount > 0
                ? $"ok:{okCount} err:{errCount}" + (timeoutCount > 0 ? $" timeout:{timeoutCount}" : "")
                : $"ok:{okCount}";
            sb.Append(summary);

            } // end try
            finally
            {
                // Only the outermost batch flushes physics — once, after all nested ops settle.
                if (--_batchDepth == 0 && !EditorApplication.isPlaying)
                {
                    Physics.SyncTransforms();
                    Physics2D.SyncTransforms();
                }
            }

            return sb.ToString().TrimEnd('\n');
        }

        internal static List<(string cmd, string argsJson)> ParseLines(string text)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrEmpty(text)) return result;

            // ExtractString doesn't unescape JSON — do it here
            text = JsonHelper.UnescapeJsonString(text);
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                var parsed = ParseLine(trimmed);
                if (!string.IsNullOrEmpty(parsed.cmd))
                    result.Add(parsed);
            }

            return result;
        }

        internal static (string cmd, string argsJson) ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return (null, null);

            // First word is command
            var firstSpace = line.IndexOf(' ');
            if (firstSpace == -1)
            {
                // Command with no args
                return (line, "{}");
            }

            var cmd = line.Substring(0, firstSpace);
            var rest = line.Substring(firstSpace + 1).Trim();

            // Parse key=value pairs into JSON
            var args = ParseKeyValuePairs(rest);
            var argsJson = BuildJsonObject(args);

            return (cmd, argsJson);
        }

        private static Dictionary<string, string> ParseKeyValuePairs(string text)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return result;

            int i = 0;
            while (i < text.Length)
            {
                // Skip whitespace
                while (i < text.Length && text[i] == ' ')
                    i++;

                if (i >= text.Length) break;

                // Find key (up to =)
                var keyStart = i;
                while (i < text.Length && text[i] != '=')
                    i++;

                if (i >= text.Length) break;

                var key = text.Substring(keyStart, i - keyStart).Trim();
                i++; // skip =

                // Parse value
                var value = ParseValue(text, ref i);
                result[key] = value;
            }

            return result;
        }

        private static string ParseValue(string text, ref int i)
        {
            if (i >= text.Length) return "";

            // Quoted value
            if (text[i] == '"')
            {
                i++; // skip opening quote
                var qStart = i;
                while (i < text.Length && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < text.Length) i++; // skip escaped char
                    i++;
                }
                var value = text.Substring(qStart, i - qStart);
                if (i < text.Length) i++; // skip closing quote
                while (i < text.Length && text[i] == ' ') i++; // skip trailing space
                return value;
            }

            // Parenthesized value e.g. (0, 6.8, 0)
            if (text[i] == '(')
            {
                var pStart = i;
                int depth = 0;
                bool closed = false;
                while (i < text.Length)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')') { depth--; if (depth == 0) { i++; closed = true; break; } }
                    i++;
                }
                if (!closed)
                    throw new Exception($"Unclosed '(' in value. Vectors like (0,0,0) must be on one line. Got: {text.Substring(pStart)}");
                while (i < text.Length && text[i] == ' ') i++;
                return text.Substring(pStart, i - pStart).TrimEnd();
            }

            // Unquoted value (up to space or end)
            var start = i;
            while (i < text.Length && text[i] != ' ') i++;
            return text.Substring(start, i - start);
        }

        private static string BuildJsonObject(Dictionary<string, string> args)
        {
            if (args.Count == 0) return "{}";
            var sb = new StringBuilder("{");
            bool first = true;

            foreach (var kvp in args)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("\"").Append(kvp.Key).Append("\":");

                sb.Append("\"").Append(JsonHelper.EscapeJson(kvp.Value)).Append("\"");
            }

            sb.Append("}");
            return sb.ToString();
        }

    }
}
