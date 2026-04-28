CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260426193717_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260426193717_InitialCreate', '10.0.7');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428133053_AddRefreshTokens') THEN
    CREATE TABLE refresh_tokens (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        token_hash text NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (NOW()),
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY (id),
        CONSTRAINT "FK_refresh_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428133053_AddRefreshTokens') THEN
    CREATE UNIQUE INDEX ix_refresh_tokens_token_hash ON refresh_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428133053_AddRefreshTokens') THEN
    CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260428133053_AddRefreshTokens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260428133053_AddRefreshTokens', '10.0.7');
    END IF;
END $EF$;
COMMIT;

