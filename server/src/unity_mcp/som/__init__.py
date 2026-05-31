"""Set-of-Mark (SoM) visual annotation package."""
from .overlay import annotate
from .extract import parse_rects

__all__ = ["annotate", "parse_rects"]
