"""Summarizer for ask() — bypass for short/single results, Haiku for complex."""

_BYPASS_CHARS = 200
_PROMPT_TEMPLATE = (
    "Answer in ≤80 tokens. Be specific (paths, counts, names). No preamble.\n"
    "If data inconclusive: \"Unknown: <why>\".\n"
    "Question: {q}\n"
    "Hint: {hint}\n"
    "Data:\n{raw}"
)


class Summarizer:
    def __init__(self, sampling_service):
        self._svc = sampling_service

    async def summarize(self, question: str, raw_results: list[str], hint: str) -> str:
        """Return summary. Bypass Haiku if single short result."""
        combined = "\n".join(raw_results)

        # Bypass: single tool + short output → return raw directly
        if len(raw_results) == 1 and len(combined) < _BYPASS_CHARS:
            return combined

        # Call Haiku
        prompt = _PROMPT_TEMPLATE.format(
            q=question,
            hint=hint,
            raw=combined[:3000],
        )
        result = await self._svc.generate(prompt, feature='summarize')
        if result:
            return result

        # Fallback to truncated raw
        return combined[:500]
