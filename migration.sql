CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260113235407_InitialCreate') THEN
    CREATE TABLE "Tales" (
        "Id" uuid NOT NULL,
        "AuthorId" uuid NOT NULL,
        "Content" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "Status" text NOT NULL,
        CONSTRAINT "PK_Tales" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260113235407_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260113235407_InitialCreate', '10.0.2');
    END IF;
END $EF$;
COMMIT;

