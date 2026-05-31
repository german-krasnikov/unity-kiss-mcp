using System;
using System.Collections.Generic;
using System.Globalization;

namespace UnityMCP.Editor
{
    internal class PlaytestState
    {
        // label → (query, capturedValue)
        readonly Dictionary<string, (string query, float value)> _captures = new();

        // invariants: list of (query, op, expected, rawLine)
        readonly List<(string query, string op, string expected, string rawLine)> _invariants = new();
        public List<string> Violations { get; } = new();

        // conserved trackers: (queries, initialSum, rawLine)
        readonly List<(string[] queries, float initialSum, string rawLine)> _conserved = new();
        public List<string> ConservedViolations { get; } = new();

        // ─── Capture ───

        public void Capture(string label, string query, float value)
            => _captures[label] = (query, value);

        public float GetCapturedValue(string label) => _captures[label].value;

        public string GetCapturedQuery(string label) => _captures[label].query;

        // ─── AssertCaptured ───

        /// <summary>Evaluate ASSERT_CAPTURED. currentValue is already read.</summary>
        public bool EvaluateCaptured(string label, string mode, string subOp, string subValue, float currentValue)
        {
            var captured = GetCapturedValue(label);
            switch (mode.ToUpperInvariant())
            {
                case "INCREASED":  return currentValue > captured;
                case "DECREASED":  return currentValue < captured;
                case "UNCHANGED":  return Math.Abs(currentValue - captured) < 0.001f;
                case "INCREASED_BY":
                case "DECREASED_BY":
                    var delta = mode.ToUpperInvariant() == "INCREASED_BY"
                        ? currentValue - captured
                        : captured - currentValue;
                    return PlaytestParser.Compare(delta.ToString(CultureInfo.InvariantCulture), subOp, subValue);
                default:
                    throw new ArgumentException($"Unknown ASSERT_CAPTURED mode: {mode}");
            }
        }

        // ─── Invariant ───

        public void RegisterInvariant(string query, string op, string expected, string rawLine)
            => _invariants.Add((query, op, expected, rawLine));

        /// <summary>Check all invariants. readValue(query) → actual string value.</summary>
        public void CheckInvariants(PlaytestConfig config, int frameCount, Func<string, string> readValue)
        {
            foreach (var (query, op, expected, rawLine) in _invariants)
            {
                try
                {
                    var actual = readValue(query);
                    if (!PlaytestParser.Compare(actual, op, expected))
                        Violations.Add($"[frame {frameCount}] INVARIANT VIOLATED: {rawLine} (actual={actual})");
                }
                catch (Exception e)
                {
                    Violations.Add($"[frame {frameCount}] INVARIANT ERR: {rawLine} — {e.Message}");
                }
            }
        }

        // ─── AssertConserved ───

        public void StartConserved(string[] queries, float duration, PlaytestConfig config,
            Func<string, string> readValue = null)
        {
            float initialSum = 0f;
            if (readValue != null)
            {
                foreach (var q in queries)
                {
                    if (float.TryParse(readValue(q), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        initialSum += v;
                }
            }
            _conserved.Add((queries, initialSum, string.Join("+", queries)));
        }

        /// <summary>Check all conserved trackers. readValue(query) → actual string value.</summary>
        public void CheckConserved(PlaytestConfig config, Func<string, string> readValue)
        {
            for (int i = 0; i < _conserved.Count; i++)
            {
                var (queries, initialSum, rawLine) = _conserved[i];
                try
                {
                    float currentSum = 0f;
                    foreach (var q in queries)
                    {
                        if (float.TryParse(readValue(q), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                            currentSum += v;
                    }
                    if (Math.Abs(currentSum - initialSum) >= 0.001f)
                        ConservedViolations.Add($"ASSERT_CONSERVED VIOLATED: SUM({rawLine}) changed {initialSum} → {currentSum}");
                }
                catch (Exception e)
                {
                    ConservedViolations.Add($"ASSERT_CONSERVED ERR: {rawLine} — {e.Message}");
                }
            }
        }

        // ─── Report ───

        public string BuildReport()
        {
            if (Violations.Count == 0 && ConservedViolations.Count == 0) return null;
            var sb = new System.Text.StringBuilder();
            foreach (var v in Violations) sb.AppendLine(v);
            foreach (var v in ConservedViolations) sb.AppendLine(v);
            return sb.ToString().TrimEnd();
        }
    }
}
