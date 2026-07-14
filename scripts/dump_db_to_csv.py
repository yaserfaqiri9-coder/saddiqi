#!/usr/bin/env python3
"""
Dump all public tables to CSV files into artifacts/db-backup-<timestamp>.
Writes artifacts/db-backup-latest.txt with the backup directory path.
Requires: psycopg2 (or psycopg2-binary) installed in the active Python environment.
"""
import os
import sys
from datetime import datetime

try:
    import psycopg2
except Exception as e:
    print("ERROR: psycopg2 not installed:", e)
    sys.exit(2)


def get_conn_params():
    # Prefer full URL env vars used by the project
    db_url = os.environ.get('DATABASE_URL') or os.environ.get('ConnectionStrings__DefaultConnection')
    if db_url:
        return db_url
    host = os.environ.get('DB_HOST') or os.environ.get('PGHOST') or 'localhost'
    port = int(os.environ.get('DB_PORT') or os.environ.get('PGPORT') or 5432)
    user = os.environ.get('DB_USER') or os.environ.get('PGUSER') or 'postgres'
    password = os.environ.get('PTG_LOCAL_DB_PASSWORD') or os.environ.get('PGPASSWORD') or ''
    dbname = os.environ.get('DB_NAME') or os.environ.get('PGDATABASE') or 'ptg_oil_system'
    return f"host={host} port={port} user={user} password={password} dbname={dbname}"


def mask(s):
    import re
    return re.sub(r'(password=)[^;]*', r'\1****', s, flags=re.IGNORECASE)


def main():
    conn_str = get_conn_params()
    print("Using connection:", mask(str(conn_str)))
    try:
        conn = psycopg2.connect(conn_str)
    except Exception as e:
        print("ERROR: failed to connect to DB:", e)
        sys.exit(3)

    cur = conn.cursor()
    cur.execute("SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename NOT LIKE 'pg_%' ORDER BY tablename")
    tables = [r[0] for r in cur.fetchall()]
    if not tables:
        print("No tables found.")
        return

    ts = datetime.utcnow().strftime('%Y%m%d%H%M%S')
    backup_dir = os.path.join('artifacts', f'db-backup-{ts}')
    os.makedirs(backup_dir, exist_ok=True)

    for table in tables:
        out_file = os.path.join(backup_dir, f"{table}.csv")
        print("Dumping", table, "->", out_file)
        try:
            with open(out_file, 'w', encoding='utf-8', newline='') as f:
                cur.copy_expert(f'COPY public."{table}" TO STDOUT WITH CSV HEADER', f)
        except Exception as e:
            print(f"Failed to dump {table}: {e}")

    cur.close()
    conn.close()

    print("Backups complete. Directory:", backup_dir)
    latest_file = os.path.join('artifacts', 'db-backup-latest.txt')
    try:
        with open(latest_file, 'w', encoding='utf-8') as lf:
            lf.write(backup_dir)
        print("Wrote", latest_file)
    except Exception as e:
        print("WARNING: could not write latest-file:", e)


if __name__ == '__main__':
    main()
#!/usr/bin/env python3
"""
Dump all public tables to CSV files into artifacts/db-backup-<timestamp>.
Creates artifacts/db-backup-latest.txt with the backup directory path.
"""
import os
import sys
from datetime import datetime

try:
    import psycopg2
except Exception as e:
    print("ERROR: psycopg2 not installed:", e)
    sys.exit(2)


def get_conn_params():
    # Prefer DATABASE_URL or ConnectionStrings__DefaultConnection
    db_url = os.environ.get('DATABASE_URL') or os.environ.get('ConnectionStrings__DefaultConnection')
    if db_url:
        # psycopg2 accepts libpq connection strings and urls; try direct connect
        return db_url
    host = os.environ.get('DB_HOST') or os.environ.get('PGHOST') or 'localhost'
    port = int(os.environ.get('DB_PORT') or os.environ.get('PGPORT') or 5432)
    user = os.environ.get('DB_USER') or os.environ.get('PGUSER') or 'postgres'
    password = os.environ.get('PTG_LOCAL_DB_PASSWORD') or os.environ.get('PGPASSWORD') or ''
    dbname = os.environ.get('DB_NAME') or os.environ.get('PGDATABASE') or 'ptg_oil_system'
    return f"host={host} port={port} user={user} password={password} dbname={dbname}"


def mask(s):
    import re
    return re.sub(r'(password=)[^;]*', r'\1****', s, flags=re.IGNORECASE)


def main():
    conn_str = get_conn_params()
    try:
        display = mask(conn_str)
    except Exception:
        display = conn_str
    print("Using connection:", display)
    try:
        conn = psycopg2.connect(conn_str)
    except Exception as e:
        print("ERROR: failed to connect to DB:", e)
        sys.exit(3)

    cur = conn.cursor()
    cur.execute("SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename NOT LIKE 'pg_%' ORDER BY tablename")
    tables = [r[0] for r in cur.fetchall()]
    if not tables:
        print("No tables found.")
        return

    ts = datetime.utcnow().strftime('%Y%m%d%H%M%S')
    backup_dir = os.path.join('artifacts', f'db-backup-{ts}')
    os.makedirs(backup_dir, exist_ok=True)

    success = True
    for table in tables:
        out_file = os.path.join(backup_dir, f"{table}.csv")
        print("Dumping", table, "->", out_file)
        try:
            with open(out_file, 'w', encoding='utf-8', newline='') as f:
                cur.copy_expert(f'COPY public."{table}" TO STDOUT WITH CSV HEADER', f)
        except Exception as e:
            print(f"Failed to dump {table}: {e}")
            success = False

    cur.close()
    conn.close()

    if success:
        print("Backups complete. Directory:", backup_dir)
    else:
        print("Backups finished with errors. Directory:", backup_dir)

    latest_file = os.path.join('artifacts', 'db-backup-latest.txt')
    try:
        with open(latest_file, 'w', encoding='utf-8') as lf:
            lf.write(backup_dir)
        print("Wrote", latest_file)
    except Exception as e:
        print("WARNING: could not write latest-file:", e)


if __name__ == '__main__':
    main()
