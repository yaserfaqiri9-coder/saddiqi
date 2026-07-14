-- Dry-run: list tables in schema 'public' that would be truncated
-- Excludes: users, __efmigrationshistory
SELECT schemaname, tablename
FROM pg_tables
WHERE schemaname = 'public'
  AND lower(tablename) NOT IN ('users','__efmigrationshistory')
ORDER BY schemaname, tablename;

-- To actually truncate, run the companion script: truncate-except-users.sql