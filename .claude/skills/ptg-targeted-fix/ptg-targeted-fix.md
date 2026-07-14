---
name: ptg-targeted-fix
description: Use for small targeted PTG Oil System fixes after the relevant files are already known.
effort: medium
---

Fix PTG Oil System issues with minimal edits.

Rules:
- Change only files directly related to the issue.
- No large refactor.
- No architecture change.
- No unrelated cleanup.
- No UI redesign unless requested.
- No database migration unless absolutely required.
- Preserve existing business logic.
- After changes, run only relevant build/test.
- Final answer must be short:
  changed files,
  reason,
  build/test result.