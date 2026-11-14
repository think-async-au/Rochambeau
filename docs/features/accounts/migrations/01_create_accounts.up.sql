-- accounts · 01_create_accounts (STAGED — not a live migration)
-- Promoted by `implement` into an EF Core migration under
-- src/Rochambeau.Api/Common/Persistence/Migrations/ (<yyyyMMddHHmmss>_AddIdentityAccounts).
-- This file is the reference DDL: `dotnet ef migrations script` for the promoted migration must
-- produce an equivalent schema (EF emits its own idempotency wrapper for the schema).
-- Source: data-model.md · accounts ADR-0001, ADR-0002, ADR-0003.

CREATE SCHEMA IF NOT EXISTS identity;

CREATE TABLE IF NOT EXISTS identity.accounts (
    id                   uuid          NOT NULL,
    user_name            varchar(20)   NOT NULL,
    normalized_user_name varchar(20)   NOT NULL,
    email                varchar(256)  NOT NULL,
    normalized_email     varchar(256)  NOT NULL,
    email_confirmed      boolean       NOT NULL,
    password_hash        text          NOT NULL,
    security_stamp       text          NOT NULL,
    concurrency_stamp    text          NOT NULL,
    two_factor_enabled   boolean       NOT NULL,
    lockout_enabled      boolean       NOT NULL,
    lockout_end          timestamptz   NULL,
    access_failed_count  integer       NOT NULL,
    role                 varchar(20)   NOT NULL,
    created_at           timestamptz   NOT NULL,
    CONSTRAINT pk_accounts PRIMARY KEY (id),
    CONSTRAINT ck_accounts_role CHECK (role IN ('player', 'administrator'))
);

-- AC-03/AC-03b race guard + AC-11/AC-12 public-profile lookup + operator command lookup.
CREATE UNIQUE INDEX IF NOT EXISTS ix_accounts_normalized_user_name
    ON identity.accounts (normalized_user_name);

-- AC-03/AC-03b race guard + AC-04/AC-05/AC-06 log-in lookup. Unique on purpose (Identity's default is not).
CREATE UNIQUE INDEX IF NOT EXISTS ix_accounts_normalized_email
    ON identity.accounts (normalized_email);
