-- ============================================================================
-- BillFolder — Schema inicial (Postgres 15+)
-- Versão correspondente a: BillFolder-ModeloDeDados-v0.2.md
-- ============================================================================
-- Notas:
--   * UUIDs são gerados PELO APP (UUID v7). O default abaixo (gen_random_uuid)
--     é só safety-net pra inserts ad-hoc. Em produção, o cliente envia o ID.
--   * updated_at é mantido por trigger.
--   * Categorias são seedadas no fim do arquivo.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- ENUMs
-- ----------------------------------------------------------------------------

CREATE TYPE income_origin_type AS ENUM (
    'work', 'rent', 'investment', 'freelance', 'gift', 'other'
);

CREATE TYPE income_status AS ENUM (
    'expected', 'received', 'late', 'not_occurred'
);

CREATE TYPE expense_status AS ENUM (
    'pending', 'paid', 'overdue'
);

CREATE TYPE card_statement_status AS ENUM (
    'open', 'closed', 'paid'
);

CREATE TYPE savings_transaction_type AS ENUM (
    'deposit', 'withdrawal', 'yield', 'transfer_out', 'transfer_in'
);

CREATE TYPE cycle_adjustment_type AS ENUM (
    'inflow', 'outflow'
);

-- ----------------------------------------------------------------------------
-- Trigger function: set updated_at
-- ----------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION trg_set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- TABELAS
-- ============================================================================

-- categories (global, seeded — não tem user_id)
CREATE TABLE categories (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    key             TEXT         NOT NULL UNIQUE,
    name_pt         TEXT         NOT NULL,
    is_system       BOOLEAN      NOT NULL DEFAULT false,
    display_order   SMALLINT     NOT NULL,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- users
CREATE TABLE users (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    email             TEXT         NOT NULL UNIQUE,
    password_hash     TEXT,
    google_oauth_id   TEXT         UNIQUE,
    display_name      TEXT         NOT NULL,
    cycle_start_rule  TEXT         NOT NULL DEFAULT '5th_business_day',
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT users_auth_method_chk
        CHECK (password_hash IS NOT NULL OR google_oauth_id IS NOT NULL)
);
CREATE TRIGGER users_set_updated_at BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- checking_accounts
CREATE TABLE checking_accounts (
    id                UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    bank_name         TEXT            NOT NULL,
    branch            TEXT,
    account_number    TEXT,
    initial_balance   NUMERIC(12,2)   NOT NULL DEFAULT 0,
    is_primary        BOOLEAN         NOT NULL DEFAULT false,
    created_at        TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX uq_checking_one_primary_per_user
    ON checking_accounts (user_id) WHERE is_primary = true;
CREATE INDEX ix_checking_user ON checking_accounts (user_id);
CREATE TRIGGER checking_accounts_set_updated_at BEFORE UPDATE ON checking_accounts
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- savings_accounts (1:0..1 com checking)
CREATE TABLE savings_accounts (
    id                    UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id               UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    checking_account_id   UUID            NOT NULL UNIQUE REFERENCES checking_accounts(id) ON DELETE CASCADE,
    bank_name             TEXT            NOT NULL,
    branch                TEXT,
    account_number        TEXT,
    initial_balance       NUMERIC(12,2)   NOT NULL DEFAULT 0,
    created_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_savings_user ON savings_accounts (user_id);
CREATE TRIGGER savings_accounts_set_updated_at BEFORE UPDATE ON savings_accounts
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- credit_card_accounts
CREATE TABLE credit_card_accounts (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name          TEXT         NOT NULL,
    issuer_bank   TEXT,
    brand         TEXT,
    closing_day   SMALLINT     NOT NULL CHECK (closing_day BETWEEN 1 AND 31),
    due_day       SMALLINT     NOT NULL CHECK (due_day BETWEEN 1 AND 31),
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_credit_cards_user ON credit_card_accounts (user_id);
CREATE TRIGGER credit_card_accounts_set_updated_at BEFORE UPDATE ON credit_card_accounts
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- cycles
CREATE TABLE cycles (
    id                       UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                  UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    start_date               DATE         NOT NULL,
    end_date                 DATE         NOT NULL,
    label                    TEXT         NOT NULL,
    is_recurrence_generated  BOOLEAN      NOT NULL DEFAULT false,
    created_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (user_id, start_date)
);
CREATE INDEX ix_cycles_user_dates ON cycles (user_id, start_date, end_date);
CREATE TRIGGER cycles_set_updated_at BEFORE UPDATE ON cycles
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- income_sources (template)
CREATE TABLE income_sources (
    id              UUID                  PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID                  NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    origin          TEXT                  NOT NULL,
    origin_type     income_origin_type    NOT NULL,
    default_amount  NUMERIC(12,2)         NOT NULL,
    expected_day    SMALLINT              NOT NULL CHECK (expected_day BETWEEN 1 AND 31),
    start_date      DATE                  NOT NULL,
    end_date        DATE,
    is_active       BOOLEAN               NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ           NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ           NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_income_sources_user ON income_sources (user_id);
CREATE TRIGGER income_sources_set_updated_at BEFORE UPDATE ON income_sources
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- income_entries (instance)
CREATE TABLE income_entries (
    id                UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    source_id         UUID            REFERENCES income_sources(id) ON DELETE SET NULL,
    expected_amount   NUMERIC(12,2)   NOT NULL,
    actual_amount     NUMERIC(12,2),
    expected_date     DATE            NOT NULL,
    actual_date       DATE,
    status            income_status   NOT NULL DEFAULT 'expected',
    notes             TEXT,
    created_at        TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at        TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_income_entries_user_date ON income_entries (user_id, expected_date);
CREATE INDEX ix_income_entries_source   ON income_entries (source_id);
CREATE TRIGGER income_entries_set_updated_at BEFORE UPDATE ON income_entries
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- expense_recurrences (template)
CREATE TABLE expense_recurrences (
    id                    UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id               UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    default_label         TEXT            NOT NULL,
    default_amount        NUMERIC(12,2)   NOT NULL,
    default_category_id   UUID            NOT NULL REFERENCES categories(id),
    due_day               SMALLINT        NOT NULL CHECK (due_day BETWEEN 1 AND 31),
    start_date            DATE            NOT NULL,
    end_date              DATE,
    is_active             BOOLEAN         NOT NULL DEFAULT true,
    created_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_expense_recurrences_user ON expense_recurrences (user_id);
CREATE TRIGGER expense_recurrences_set_updated_at BEFORE UPDATE ON expense_recurrences
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- card_statements (criada antes de expenses pra evitar circular FK)
CREATE TABLE card_statements (
    id                  UUID                    PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID                    NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    card_id             UUID                    NOT NULL REFERENCES credit_card_accounts(id) ON DELETE CASCADE,
    period_start        DATE                    NOT NULL,
    period_end          DATE                    NOT NULL,
    due_date            DATE                    NOT NULL,
    status              card_statement_status   NOT NULL DEFAULT 'open',
    linked_expense_id   UUID,  -- FK adicionada após criar expenses
    created_at          TIMESTAMPTZ             NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ             NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_card_statements_card_due ON card_statements (card_id, due_date);
CREATE INDEX ix_card_statements_user ON card_statements (user_id);
CREATE TRIGGER card_statements_set_updated_at BEFORE UPDATE ON card_statements
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- expenses (instance)
CREATE TABLE expenses (
    id                          UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                     UUID             NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    template_id                 UUID             REFERENCES expense_recurrences(id) ON DELETE SET NULL,
    due_date                    DATE             NOT NULL,
    label                       TEXT             NOT NULL,
    expected_amount             NUMERIC(12,2)    NOT NULL,
    actual_amount               NUMERIC(12,2),
    status                      expense_status   NOT NULL DEFAULT 'pending',
    paid_date                   DATE,
    paid_from_account_id        UUID             REFERENCES checking_accounts(id) ON DELETE SET NULL,
    category_id                 UUID             NOT NULL REFERENCES categories(id),
    linked_card_statement_id    UUID             REFERENCES card_statements(id) ON DELETE SET NULL,
    notes                       TEXT,
    created_at                  TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    updated_at                  TIMESTAMPTZ      NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_expenses_user_due ON expenses (user_id, due_date);
CREATE INDEX ix_expenses_template ON expenses (template_id);
CREATE INDEX ix_expenses_status ON expenses (user_id, status) WHERE status = 'overdue';
CREATE TRIGGER expenses_set_updated_at BEFORE UPDATE ON expenses
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- Agora a FK reversa de card_statements → expenses
ALTER TABLE card_statements
    ADD CONSTRAINT fk_card_statements_linked_expense
    FOREIGN KEY (linked_expense_id) REFERENCES expenses(id) ON DELETE SET NULL;

-- daily_expense_recurrences (template)
CREATE TABLE daily_expense_recurrences (
    id                    UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id               UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    default_label         TEXT            NOT NULL,
    default_amount        NUMERIC(12,2)   NOT NULL,
    default_category_id   UUID            NOT NULL REFERENCES categories(id),
    default_account_id    UUID            NOT NULL REFERENCES checking_accounts(id) ON DELETE CASCADE,
    day_of_month          SMALLINT        NOT NULL CHECK (day_of_month BETWEEN 1 AND 31),
    start_date            DATE            NOT NULL,
    end_date              DATE,
    is_active             BOOLEAN         NOT NULL DEFAULT true,
    created_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_daily_expense_recurrences_user ON daily_expense_recurrences (user_id);
CREATE TRIGGER daily_expense_recurrences_set_updated_at BEFORE UPDATE ON daily_expense_recurrences
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- daily_expenses (instance)
CREATE TABLE daily_expenses (
    id            UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       UUID             NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    template_id   UUID             REFERENCES daily_expense_recurrences(id) ON DELETE SET NULL,
    date          DATE             NOT NULL,
    category_id   UUID             NOT NULL REFERENCES categories(id),
    label         TEXT             NOT NULL,
    amount        NUMERIC(12,2)    NOT NULL,
    account_id    UUID             NOT NULL REFERENCES checking_accounts(id) ON DELETE CASCADE,
    notes         TEXT,
    created_at    TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ      NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_daily_expenses_user_date ON daily_expenses (user_id, date);
CREATE INDEX ix_daily_expenses_template ON daily_expenses (template_id);
CREATE TRIGGER daily_expenses_set_updated_at BEFORE UPDATE ON daily_expenses
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- card_entry_recurrences (template)
CREATE TABLE card_entry_recurrences (
    id                    UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id               UUID            NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    card_id               UUID            NOT NULL REFERENCES credit_card_accounts(id) ON DELETE CASCADE,
    default_label         TEXT            NOT NULL,
    default_amount        NUMERIC(12,2)   NOT NULL,
    default_category_id   UUID            NOT NULL REFERENCES categories(id),
    day_of_month          SMALLINT        NOT NULL CHECK (day_of_month BETWEEN 1 AND 31),
    start_date            DATE            NOT NULL,
    end_date              DATE,
    is_active             BOOLEAN         NOT NULL DEFAULT true,
    created_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_card_entry_recurrences_user ON card_entry_recurrences (user_id);
CREATE TRIGGER card_entry_recurrences_set_updated_at BEFORE UPDATE ON card_entry_recurrences
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- card_entries (instance)
CREATE TABLE card_entries (
    id                  UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID             NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    card_id             UUID             NOT NULL REFERENCES credit_card_accounts(id) ON DELETE CASCADE,
    template_id         UUID             REFERENCES card_entry_recurrences(id) ON DELETE SET NULL,
    purchase_date       DATE             NOT NULL,
    label               TEXT             NOT NULL,
    total_amount        NUMERIC(12,2)    NOT NULL CHECK (total_amount >= 0),
    installments_count  SMALLINT         NOT NULL CHECK (installments_count >= 1),
    category_id         UUID             NOT NULL REFERENCES categories(id),
    notes               TEXT,
    created_at          TIMESTAMPTZ      NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ      NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_card_entries_user_date ON card_entries (user_id, purchase_date);
CREATE INDEX ix_card_entries_card ON card_entries (card_id);
CREATE INDEX ix_card_entries_template ON card_entries (template_id);
CREATE TRIGGER card_entries_set_updated_at BEFORE UPDATE ON card_entries
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- installments
CREATE TABLE installments (
    id                  UUID             PRIMARY KEY DEFAULT gen_random_uuid(),
    card_entry_id       UUID             NOT NULL REFERENCES card_entries(id) ON DELETE CASCADE,
    statement_id        UUID             NOT NULL REFERENCES card_statements(id) ON DELETE CASCADE,
    installment_number  SMALLINT         NOT NULL CHECK (installment_number >= 1),
    amount              NUMERIC(12,2)    NOT NULL,
    UNIQUE (card_entry_id, installment_number)
);
CREATE INDEX ix_installments_statement ON installments (statement_id);

-- savings_transactions
CREATE TABLE savings_transactions (
    id                       UUID                       PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                  UUID                       NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    savings_account_id       UUID                       NOT NULL REFERENCES savings_accounts(id) ON DELETE CASCADE,
    type                     savings_transaction_type   NOT NULL,
    amount                   NUMERIC(12,2)              NOT NULL CHECK (amount >= 0),
    date                     DATE                       NOT NULL,
    label                    TEXT,
    linked_transaction_id    UUID                       REFERENCES savings_transactions(id) ON DELETE SET NULL,
    created_at               TIMESTAMPTZ                NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ                NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_savings_transactions_account_date ON savings_transactions (savings_account_id, date);
CREATE INDEX ix_savings_transactions_user_date ON savings_transactions (user_id, date);
CREATE TRIGGER savings_transactions_set_updated_at BEFORE UPDATE ON savings_transactions
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- cycle_adjustments
CREATE TABLE cycle_adjustments (
    id                                  UUID                      PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                             UUID                      NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type                                cycle_adjustment_type     NOT NULL,
    label                               TEXT                      NOT NULL,
    amount                              NUMERIC(12,2)             NOT NULL CHECK (amount >= 0),
    date                                DATE                      NOT NULL,
    source_savings_transaction_id       UUID                      REFERENCES savings_transactions(id) ON DELETE SET NULL,
    created_at                          TIMESTAMPTZ               NOT NULL DEFAULT NOW(),
    updated_at                          TIMESTAMPTZ               NOT NULL DEFAULT NOW()
);
CREATE INDEX ix_cycle_adjustments_user_date ON cycle_adjustments (user_id, date);
CREATE TRIGGER cycle_adjustments_set_updated_at BEFORE UPDATE ON cycle_adjustments
    FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- ============================================================================
-- SEED: categorias
-- ============================================================================

INSERT INTO categories (key, name_pt, is_system, display_order) VALUES
    ('groceries',     'Mercado',                  false,  1),
    ('shopping',      'Shopping',                 false,  2),
    ('fuel',          'Combustível',              false,  3),
    ('bills',         'Contas',                   false,  4),
    ('fun',           'Lazer',                    false,  5),
    ('food',          'Alimentação',              false,  6),
    ('parking_tolls', 'Estacionamento e Pedágio', false,  7),
    ('self_care',     'Cuidado pessoal',          false,  8),
    ('transport',     'Transporte',               false,  9),
    ('health',        'Saúde',                    false, 10),
    ('education',     'Educação',                 false, 11),
    ('travel',        'Viagem',                   false, 12),
    ('gifts',         'Presentes',                false, 13),
    ('donations',     'Doações',                  false, 14),
    ('pets',          'Pets',                     false, 15),
    ('home',          'Casa',                     false, 16),
    ('subscriptions', 'Assinaturas digitais',     false, 17),
    ('credit_card',   'Cartão de Crédito',        true,  99);

-- ============================================================================
-- FIM
-- ============================================================================
