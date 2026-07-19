# Filter state machine

```text
IDLE
  └─ focus input ───────────────> FIELD_MENU

FIELD_MENU
  ├─ type text ─────────────────> FIELD_MENU (field list is filtered)
  ├─ Enter / search row ────────> APPLY_FREE_TEXT
  └─ choose field ──────────────> OPERATOR_MENU

OPERATOR_MENU
  ├─ choose = / != / in ───────> VALUE_MENU
  └─ choose date/range ─────────> DATE_PANEL

VALUE_MENU
  ├─ choose single value ───────> COMMIT_TOKEN → FIELD_MENU
  └─ choose multiple values
       └─ Apply ────────────────> COMMIT_TOKEN → FIELD_MENU

DATE_PANEL
  └─ Apply ─────────────────────> COMMIT_TOKEN → FIELD_MENU

GLOBAL
  ├─ click outside / Escape ────> CLOSED
  ├─ remove token ──────────────> field becomes available again
  ├─ Clear all ─────────────────> IDLE + APPLY
  └─ Enter ─────────────────────> APPLY
```

# Query grammar used by the demo serializer

```text
search="free text"
status:active
not status:inactive
country:AF,RU,TM
created_at>=2026-01-01 created_at<=2026-07-13
```

For production, submit the structured `tokens` array rather than parsing the display string.
