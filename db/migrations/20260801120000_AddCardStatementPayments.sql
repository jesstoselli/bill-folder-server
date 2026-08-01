BEGIN;

ALTER TABLE card_statements ADD COLUMN IF NOT EXISTS paid_date DATE;
ALTER TABLE card_statements ADD COLUMN IF NOT EXISTS actual_amount NUMERIC(12,2);
ALTER TABLE card_statements ADD COLUMN IF NOT EXISTS paid_from_account_id UUID;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_card_statements_paid_from_account'
          AND conrelid = 'public.card_statements'::regclass
          AND contype = 'f'
    ) THEN
        ALTER TABLE card_statements
            ADD CONSTRAINT fk_card_statements_paid_from_account
            FOREIGN KEY (paid_from_account_id)
            REFERENCES checking_accounts(id) ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_card_statements_paid_from_account
    ON card_statements (paid_from_account_id);

COMMIT;
