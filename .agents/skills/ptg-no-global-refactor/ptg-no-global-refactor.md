---
name: ptg-no-global-refactor
description: Use when Claude must avoid large refactors and only fix the requested PTG issue.
effort: low
---

Prevent unnecessary refactor.

Rules:
- Do not rename classes, services, entities, routes, CSS classes, or files globally.
- Do not move files.
- Do not change architecture.
- Do not cleanup unrelated warnings.
- Do not change unrelated UI.
- Fix only the requested issue.
- If larger refactor is needed, stop and ask for approval.