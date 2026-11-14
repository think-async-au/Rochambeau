-- accounts · 01_create_accounts — rollback (STAGED — not a live migration)
-- EF Core's generated Down drops the table but NOT the schema: add migrationBuilder.DropSchema("identity")
-- to the promoted migration's Down so the rollback is complete.

DROP INDEX IF EXISTS identity.ix_accounts_normalized_email;
DROP INDEX IF EXISTS identity.ix_accounts_normalized_user_name;
DROP TABLE IF EXISTS identity.accounts;
-- RESTRICT (the default): fails loudly if anything else still lives in the identity schema.
DROP SCHEMA IF EXISTS identity;
