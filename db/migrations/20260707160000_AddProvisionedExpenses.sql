-- 20260707160000_AddProvisionedExpenses
-- Despesas provisionadas com baixa semanal (terapia, diarista).
--
-- Adiciona:
--  - tipo enum expense_recurrence_frequency (monthly | weekly)
--  - cadência em expense_recurrences (frequency, weekday) + due_day passa a nullable
--  - campos de ocorrência em expenses (occurrence_amount, occurrences_total,
--    occurrences_paid, paid_to_date) — nullable/zero em despesas normais.
--
-- Idempotente (IF NOT EXISTS / DROP IF EXISTS). Rodar direto no Postgres de prod.
-- NOTA: escrita à mão (não gerada por `dotnet ef migrations script`) porque o
-- snapshot do EF tem anotações de enum duplicadas (com e sem prefixo `public.`)
-- que fazem o `migrations add` emitir churn não-relacionado. Ver dívida técnica.

BEGIN;

-- 1. Novo tipo enum (Postgres não tem CREATE TYPE IF NOT EXISTS)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'expense_recurrence_frequency') THEN
        CREATE TYPE expense_recurrence_frequency AS ENUM ('monthly', 'weekly');
    END IF;
END $$;

-- 2. expenses: campos de provisionamento
ALTER TABLE expenses ADD COLUMN IF NOT EXISTS occurrence_amount NUMERIC(12,2);
ALTER TABLE expenses ADD COLUMN IF NOT EXISTS occurrences_total INTEGER;
ALTER TABLE expenses ADD COLUMN IF NOT EXISTS occurrences_paid  INTEGER       NOT NULL DEFAULT 0;
ALTER TABLE expenses ADD COLUMN IF NOT EXISTS paid_to_date      NUMERIC(12,2) NOT NULL DEFAULT 0;

-- 3. expense_recurrences: cadência
ALTER TABLE expense_recurrences ADD COLUMN IF NOT EXISTS frequency expense_recurrence_frequency NOT NULL DEFAULT 'monthly';
ALTER TABLE expense_recurrences ADD COLUMN IF NOT EXISTS weekday   SMALLINT;
ALTER TABLE expense_recurrences ALTER COLUMN due_day DROP NOT NULL;

-- 4. Constraints nomeadas (casa com a config EF). O CHECK antigo de due_day, se
--    existir com nome auto-gerado, é compatível (CHECK passa em NULL no Postgres).
ALTER TABLE expense_recurrences DROP CONSTRAINT IF EXISTS ck_due_day;
ALTER TABLE expense_recurrences ADD  CONSTRAINT ck_due_day CHECK (due_day IS NULL OR (due_day BETWEEN 1 AND 31));
ALTER TABLE expense_recurrences DROP CONSTRAINT IF EXISTS ck_weekday;
ALTER TABLE expense_recurrences ADD  CONSTRAINT ck_weekday CHECK (weekday IS NULL OR (weekday BETWEEN 0 AND 6));

COMMIT;
