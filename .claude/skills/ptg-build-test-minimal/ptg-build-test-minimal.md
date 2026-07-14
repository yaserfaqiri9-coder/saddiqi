---
name: ptg-build-test-minimal
description: Use after PTG code changes to run only the minimum useful build and tests.
effort: low
---

Run minimal verification.

Rules:
- Do not run all tests unless necessary.
- First run targeted build.
- Then run only related tests.
- If full test is needed, explain why first.
- Do not run slow scans unless requested.
- Report only:
  build status,
  test status,
  errors if any,
  next safe action.