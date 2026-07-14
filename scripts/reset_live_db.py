import psycopg2

DB_HOST = "localhost"
DB_PORT = 5432
DB_USER = "postgres"
DB_PASSWORD = "__test_password__"
DB_NAME = "ptg_oil_system"

conn = psycopg2.connect(
    host=DB_HOST,
    port=DB_PORT,
    user=DB_USER,
    password=DB_PASSWORD,
    dbname=DB_NAME,
)

try:
    cur = conn.cursor()
    cur.execute(
        """
        SELECT table_schema, table_name
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_type = 'BASE TABLE'
          AND table_name NOT IN ('Users', '__EFMigrationsHistory')
        ORDER BY table_schema, table_name
        """
    )
    tables = [(row[0], row[1]) for row in cur.fetchall()]

    if not tables:
        print("No tables to truncate.")
        raise SystemExit(0)

    identifiers = [f'"{schema}"."{table}"' for schema, table in tables]
    sql = "TRUNCATE TABLE " + ", ".join(identifiers) + " RESTART IDENTITY CASCADE;"

    print("TRUNCATING_TABLES", len(tables))
    print(sql)
    cur.execute(sql)
    conn.commit()

    cur.execute('SELECT COUNT(*) FROM public."Users"')
    users_count = cur.fetchone()[0]
    print("USERS_REMAINING", users_count)

    cur.close()
finally:
    conn.close()
