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
    ('Clínica Bem Viver',    3, 'Semestral',  'Boleto',            NOW() - INTERVAL '30 days',  NOW() + INTERVAL '150 days', 'Ativo'),
    ('Livraria Página 2',    2, 'Mensal',     'Cartão de Crédito', NOW() - INTERVAL '25 days',  NOW() + INTERVAL '5 days',   'Ativo'),
    ('Pet Shop Amigo Fiel',  1, 'Trimestral', 'Pix',               NOW() - INTERVAL '120 days', NOW() - INTERVAL '30 days',  'Inativo'),
    ('Escola de Idiomas Go', 3, 'Anual',      'Boleto',            NOW() - INTERVAL '10 days',  NOW() + INTERVAL '355 days', 'Ativo');
