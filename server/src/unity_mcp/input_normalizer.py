"""Normalize Python-native values to C# text protocol form (set_property only).

Phase 1 scope: bool/None/list. Int 0 is NOT coerced to "false" — passes as "0".
Agents passing int 0 to a bool field will hit C# bool.Parse error (acceptable: rare,
diagnostic). Phase 2 may add schema-aware int→bool coercion if needed.
"""

_BOOL_TRUE = {"true", "1", "yes", "on"}
_BOOL_FALSE = {"false", "0", "no", "off"}


def normalize_value(v) -> str:
    if v is True:
        return "true"
    if v is False:
        return "false"
    if v is None:
        return "null"
    if isinstance(v, (list, tuple)):
        return ",".join(normalize_value(x) for x in v)
    if isinstance(v, str):
        low = v.strip().lower()
        if low in _BOOL_TRUE:
            return "true"
        if low in _BOOL_FALSE:
            return "false"
        return v
    return str(v)
