# PTG Oil System

## Technology
ASP.NET Core MVC .NET 8, EF Core, PostgreSQL, Razor Views, Bootstrap 5 RTL.

## Mandatory Rules
- Read and understand the existing code before making changes.
- Only modify files directly related to the current request.
- Do not refactor or redesign unrelated areas.
- Do not change Entity, Migration, DbContext, or database structure unless explicitly requested.
- Do not change Stock, Inventory, Ledger, Payment, Sales, FX, or P&L logic unless explicitly requested.
- Never guess business behavior.
- First identify the root cause and related files.
- Make the smallest safe change.
- Run build and relevant tests after changes.
- Keep final responses short and precise.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
