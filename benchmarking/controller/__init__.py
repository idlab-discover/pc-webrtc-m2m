"""Controller package marker.

This file turns `controller/` into a Python package. Shared utilities (the
configuration dataclasses and parsing helpers) were moved to the top-level
`shared` package — import them via `shared.config`.
"""

__all__ = ["server"]
