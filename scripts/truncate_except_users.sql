BEGIN;

DO $$
DECLARE
    t RECORD;
    excluded TEXT[] := ARRAY[
        'Users',
        'Roles',
        'Permissions',
        'RolePermissions',
        '__EFMigrationsHistory'
    ];
BEGIN
    FOR t IN
        SELECT schemaname, tablename
        FROM pg_tables
        WHERE schemaname = 'public'
          AND tablename <> ALL (excluded)
    LOOP
        RAISE NOTICE 'Truncating table: %', t.tablename;
        EXECUTE format('TRUNCATE TABLE %I.%I CASCADE;', t.schemaname, t.tablename);
    END LOOP;
END
$$;

COMMIT;
