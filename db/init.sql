-- =============================================================
-- Assina+ | Estrutura do banco e dados de demonstração
-- Executado automaticamente pelo docker-compose na primeira subida.
-- =============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS Planos (
    Id          SERIAL PRIMARY KEY,
    Nome        VARCHAR(100)   NOT NULL,
    Valor       DECIMAL(10, 2) NOT NULL,
    Descricao   TEXT,
    Ativo       BOOLEAN        NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS Usuarios (
    Id                SERIAL PRIMARY KEY,
    Nome              VARCHAR(100) NOT NULL,
    Email             VARCHAR(150) NOT NULL UNIQUE,
    SenhaHash         TEXT         NOT NULL,
    CPF               VARCHAR(14)  NOT NULL DEFAULT '',
    Cargo             VARCHAR(50)  NOT NULL DEFAULT 'Colaborador',
    DataCriacao       TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    EmailConfirmado   BOOLEAN      NOT NULL DEFAULT TRUE,
    TokenConfirmacao  TEXT
);

CREATE TABLE IF NOT EXISTS Assinaturas (
    Id               SERIAL PRIMARY KEY,
    ClienteNome      VARCHAR(100) NOT NULL,
    PlanoId          INT          NOT NULL REFERENCES Planos(Id),
    TipoRecorrencia  VARCHAR(20)  NOT NULL,   -- Mensal, Trimestral, Semestral, Anual
    FormaPagamento   VARCHAR(50)  NOT NULL,
    DataInicio       TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DataExpiracao    TIMESTAMP,
    Status           VARCHAR(20)  NOT NULL DEFAULT 'Ativo'  -- Ativo, Inativo
);

-- Uma cobrança por período da assinatura. O vencimento é o início do período (cobrança antecipada).
CREATE TABLE IF NOT EXISTS Cobrancas (
    Id              SERIAL PRIMARY KEY,
    AssinaturaId    INT            NOT NULL REFERENCES Assinaturas(Id),
    PeriodoInicio   DATE           NOT NULL,
    PeriodoFim      DATE           NOT NULL,
    DataVencimento  DATE           NOT NULL,
    Valor           DECIMAL(10, 2) NOT NULL,
    Status          VARCHAR(20)    NOT NULL DEFAULT 'Pendente',  -- Pendente, Vencida, Paga, Cancelada
    DataPagamento   TIMESTAMP,
    FormaPagamento  VARCHAR(50),
    DataCriacao     TIMESTAMP      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT UQ_Cobrancas_Periodo UNIQUE (AssinaturaId, PeriodoInicio)
);

CREATE INDEX IF NOT EXISTS IX_Cobrancas_Status_Vencimento ON Cobrancas (Status, DataVencimento);

-- -------------------------------------------------------------
-- Dados de demonstração
-- -------------------------------------------------------------

INSERT INTO Planos (Nome, Valor, Descricao, Ativo) VALUES
    ('Básico',       29.90,  'Ideal para começar: até 100 assinantes e relatórios mensais.', TRUE),
    ('Profissional', 79.90,  'Assinantes ilimitados, relatórios semanais e suporte por e-mail.', TRUE),
    ('Empresarial',  199.90, 'Tudo do Profissional, integrações via API e suporte prioritário.', TRUE);

-- Senhas geradas com bcrypt (compatível com BCrypt.Net usado pela API)
INSERT INTO Usuarios (Nome, Email, SenhaHash, Cargo) VALUES
    ('Conta Demo',     'demo@assinamais.dev',        crypt('demo123', gen_salt('bf', 11)), 'Admin'),
    ('Ana Colaboradora', 'colaborador@assinamais.dev', crypt('demo123', gen_salt('bf', 11)), 'Colaborador');

INSERT INTO Assinaturas (ClienteNome, PlanoId, TipoRecorrencia, FormaPagamento, DataInicio, DataExpiracao, Status) VALUES
    ('Padaria Pão Quente',   1, 'Mensal',     'Pix',               NOW() - INTERVAL '20 days',  NOW() + INTERVAL '10 days',  'Ativo'),
    ('Studio Fit',           2, 'Anual',      'Cartão de Crédito', NOW() - INTERVAL '60 days',  NOW() + INTERVAL '305 days', 'Ativo'),
    ('Clínica Bem Viver',    3, 'Semestral',  'Boleto',            NOW() - INTERVAL '45 days',  NOW() + INTERVAL '137 days', 'Ativo'),
    ('Livraria Página 2',    2, 'Mensal',     'Cartão de Crédito', NOW() - INTERVAL '25 days',  NOW() + INTERVAL '5 days',   'Ativo'),
    ('Pet Shop Amigo Fiel',  1, 'Trimestral', 'Pix',               NOW() - INTERVAL '120 days', NOW() - INTERVAL '30 days',  'Inativo'),
    ('Escola de Idiomas Go', 3, 'Anual',      'Boleto',            NOW() - INTERVAL '10 days',  NOW() + INTERVAL '355 days', 'Ativo');

-- Cobranças já emitidas para as assinaturas acima. Valores seguem a regra de ciclos:
-- mensal 1x, trimestral 3x, semestral 5x (1 mês grátis) e anual 10x (2 meses grátis).
-- A API gera os próximos ciclos sozinha (CobrancaWorker).
INSERT INTO Cobrancas (AssinaturaId, PeriodoInicio, PeriodoFim, DataVencimento, Valor, Status, DataPagamento, FormaPagamento)
SELECT a.Id,
       a.DataInicio::date,
       (a.DataInicio::date + (c.Meses || ' months')::interval)::date,
       a.DataInicio::date,
       c.Valor, c.Status,
       CASE WHEN c.Status = 'Paga' THEN a.DataInicio + INTERVAL '1 day' END,
       CASE WHEN c.Status = 'Paga' THEN a.FormaPagamento END
FROM Assinaturas a
JOIN (VALUES
    ('Padaria Pão Quente',   1,  29.90,   'Paga'),
    ('Studio Fit',           12, 799.00,  'Paga'),
    ('Clínica Bem Viver',    6,  999.50,  'Vencida'),
    ('Livraria Página 2',    1,  79.90,   'Vencida'),
    ('Pet Shop Amigo Fiel',  3,  89.70,   'Vencida'),
    ('Escola de Idiomas Go', 12, 1999.00, 'Paga')
) AS c (ClienteNome, Meses, Valor, Status) ON c.ClienteNome = a.ClienteNome;
