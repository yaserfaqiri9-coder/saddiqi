-- Dry-run: فهرست جدول‌هایی که خالی خواهند شد (به جز 'users' و '__efmigrationshistory')
-- این فایل را اول اجرا کنید تا فهرست جداول را بررسی کنید.

SELECT schemaname, tablename
FROM pg_tables
WHERE schemaname = 'public'
  AND lower(tablename) NOT IN ('users', '__efmigrationshistory')
ORDER BY schemaname, tablename;
