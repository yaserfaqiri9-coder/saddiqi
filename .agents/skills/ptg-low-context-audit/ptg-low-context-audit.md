---
name: ptg-low-context-audit
description: Use for PTG Oil System bug/feature analysis when the goal is to reduce Claude context usage by reading only the minimum needed files.
effort: low
---

Audit PTG Oil System with minimum context.

Rules:
- Do not scan the whole repository.
- Do not edit files.
- First identify only 3-7 likely files.
- Read only targeted files.
- Prefer Grep/Glob before opening large files.
- Avoid reading generated/build/cache folders.
- Output must be short:
  1. suspected cause
  2. exact files needed
  3. safe next step