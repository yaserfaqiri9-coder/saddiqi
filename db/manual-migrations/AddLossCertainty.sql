START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605010907_AddLossCertainty') THEN
    ALTER TABLE "LossEvents" ADD "LossCertainty" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605010907_AddLossCertainty') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260605010907_AddLossCertainty', '8.0.10');
    END IF;
END $EF$;
COMMIT;

