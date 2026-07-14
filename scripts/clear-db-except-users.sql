-- clear-db-except-users.sql
-- Warning: This script will REMOVE data from all public schema tables
-- except the table named 'users' (and '__efmigrationshistory').
-- Make a full backup before running. Example backup command:
-- pg_dump -h <host> -p <port> -U <user> -F c -b -v -f "backup-$(date +%Y%m%d%H%M%S).dump" <database>
-- Usage (psql):
-- psql -h <host> -p <port> -U <user> -d <database> -f scripts/clear-db-except-users.sql

BEGIN;

DO
$$
DECLARE
    r RECORD;
    exclude_tables text[] := ARRAY['users', '__efmigrationshistory'];
BEGIN
    FOR r IN
        SELECT tablename FROM pg_tables
        WHERE schemaname = 'public'
          AND tablename <> ALL (exclude_tables)
          AND tablename NOT LIKE 'pg_%'
    LOOP
        RAISE NOTICE 'Truncating table: %', r.tablename;
        EXECUTE format('TRUNCATE TABLE public.%I RESTART IDENTITY CASCADE', r.tablename);
    END LOOP;
END
$$;

COMMIT;

-- Note:
-- - This script uses TRUNCATE ... RESTART IDENTITY CASCADE which resets sequences
--   and cascades to dependent tables. It is fast but destructive.
-- - If you want to exclude additional tables (e.g. lookup tables), add their names
--   to the "exclude_tables" array above.
