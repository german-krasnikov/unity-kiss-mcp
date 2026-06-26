using System;
using System.Globalization;

namespace UnityMCP.Editor
{
    internal static class WatchCondition
    {
        // Evaluate("< 10", 8.5f) → true
        // Evaluate("== null", null) → true
        // Evaluate("", 5f) → false
        public static bool Evaluate(string condition, object value)
        {
            if (string.IsNullOrEmpty(condition)) return false;
            condition = condition.Trim();
            // Fix Unity fake-null: destroyed UnityEngine.Object boxes to non-null C# reference
            if (value is UnityEngine.Object uo && uo == null) value = null;

            var (op, rhs) = ParseOp(condition);
            if (op == null) return false;

            // Null checks
            if (rhs == "null")
                return op == "==" ? value == null : op == "!=" ? value != null : false;

            if (value == null) return false;

            // Normalize float representation for comparison
            string valueStr = value is float f
                ? f.ToString("G", CultureInfo.InvariantCulture)
                : value is double d
                    ? d.ToString("G", CultureInfo.InvariantCulture)
                    : value is IFormattable iform
                        ? iform.ToString(null, CultureInfo.InvariantCulture)
                        : value.ToString();

            bool rhsNum = float.TryParse(rhs, NumberStyles.Float, CultureInfo.InvariantCulture, out float rhsF);
            bool lhsNum = float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float lhsF);

            if (rhsNum && lhsNum)
                return op switch
                {
                    "<"  => lhsF < rhsF,
                    ">"  => lhsF > rhsF,
                    "<=" => lhsF <= rhsF,
                    ">=" => lhsF >= rhsF,
                    "==" => Math.Abs(lhsF - rhsF) < 1e-5f,
                    "!=" => Math.Abs(lhsF - rhsF) >= 1e-5f,
                    _    => false
                };

            // String fallback — only == and != meaningful
            return op switch
            {
                "==" => string.Equals(valueStr, rhs, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(valueStr, rhs, StringComparison.OrdinalIgnoreCase),
                _    => false
            };
        }

        private static (string op, string rhs) ParseOp(string cond)
        {
            // Check two-char operators first to avoid prefix conflict (<= vs <)
            foreach (var o in new[] { "<=", ">=", "!=", "==", "<", ">" })
                if (cond.StartsWith(o))
                    return (o, cond.Substring(o.Length).Trim());
            return (null, null);
        }
    }
}
