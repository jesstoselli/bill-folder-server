-- Estornos no cartão: card_entries.total_amount pode ser negativo, desde que à
-- vista. Idempotente. O CHECK antigo existe com o nome da config EF
-- (ck_total_amount_nonneg) ou com o nome gerado pelo schema.sql.
START TRANSACTION;

ALTER TABLE card_entries DROP CONSTRAINT IF EXISTS ck_total_amount_nonneg;
ALTER TABLE card_entries DROP CONSTRAINT IF EXISTS card_entries_total_amount_check;
ALTER TABLE card_entries DROP CONSTRAINT IF EXISTS ck_total_amount_refund_single;
ALTER TABLE card_entries ADD  CONSTRAINT ck_total_amount_refund_single
    CHECK (total_amount >= 0 OR installments_count = 1);

COMMIT;
