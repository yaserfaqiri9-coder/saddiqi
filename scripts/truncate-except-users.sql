-- Truncate all tables in public schema except 'users' and '__EFMigrationsHistory'
-- WARNING: This will DELETE ALL DATA in those tables and RESET sequences.
-- Make a backup first. Example backup command using pg_dump is in README.

BEGIN;

DO
$$
DECLARE
    tbls text;
BEGIN
    SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
    INTO tbls
    FROM pg_tables
    WHERE schemaname = 'public'
      AND lower(tablename) NOT IN ('users','__efmigrationshistory');

    IF tbls IS NULL THEN
        RAISE NOTICE 'No tables to truncate';
    ELSE
        EXECUTE 'TRUNCATE ' || tbls || ' RESTART IDENTITY CASCADE';
        RAISE NOTICE 'Truncated: %', tbls;
    END IF;
END;
$$;

COMMIT;

-- Notes:
-- - This script uses RESTART IDENTITY to reset sequences and CASCADE to handle FK dependencies.
-- - It excludes 'users' and '__EFMigrationsHistory'. Adjust the exclusion list if needed.