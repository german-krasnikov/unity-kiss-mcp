"""Issue 27: single source of truth for "problem" console log levels.

Unity splits logs into 5 LogTypes: Error, Assert, Warning, Log, Exception.
Unhandled C# exceptions arrive as Exception, not Error — callers that hardcoded
level="Error" silently missed them. Use this constant everywhere a caller wants
"is there a problem?" rather than the narrower "is there an Error?".
"""

PROBLEM_LEVELS = "Error,Exception,Assert"
