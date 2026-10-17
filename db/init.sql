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
    ClienteEmail     VARCHAR(150),             -- opcional; sem e-mail não há lembretes
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

-- Histórico de lembretes enviados. Os automáticos são únicos por cobrança e tipo; os manuais podem se repetir.
CREATE TABLE IF NOT EXISTS LembretesCobranca (
    Id          SERIAL PRIMARY KEY,
    CobrancaId  INT          NOT NULL REFERENCES Cobrancas(Id),
    Tipo        VARCHAR(30)  NOT NULL,   -- AVISO_VENCIMENTO, COBRANCA_VENCIDA, MANUAL
    DataEnvio   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_LembretesCobranca_Automaticos
    ON LembretesCobranca (CobrancaId, Tipo) WHERE Tipo <> 'MANUAL';

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

-- E-mails fictícios no domínio reservado .example (RFC 2606): nunca chegam a ninguém de verdade
INSERT INTO Assinaturas (ClienteNome, PlanoId, TipoRecorrencia, FormaPagamento, DataInicio, Status) VALUES
    ('Padaria Pão Quente',     1, 'Mensal',     'Pix',               NOW() - INTERVAL '20 days',  'Ativo'),
    ('Studio Fit',             2, 'Anual',      'Cartão de Crédito', NOW() - INTERVAL '60 days',  'Ativo'),
    ('Clínica Bem Viver',      3, 'Semestral',  'Boleto',            NOW() - INTERVAL '45 days',  'Ativo'),
    ('Livraria Página 2',      2, 'Mensal',     'Cartão de Crédito', NOW() - INTERVAL '25 days',  'Ativo'),
    ('Pet Shop Amigo Fiel',    1, 'Trimestral', 'Pix',               NOW() - INTERVAL '120 days', 'Inativo'),
    ('Escola de Idiomas Go',   3, 'Anual',      'Boleto',            NOW() - INTERVAL '10 days',  'Ativo'),
    ('Café Grão Nobre',        2, 'Mensal',     'Cartão de Crédito', NOW() - INTERVAL '100 days', 'Ativo'),
    ('Academia Ritmo',         3, 'Mensal',     'Boleto',            NOW() - INTERVAL '80 days',  'Ativo'),
    ('Barbearia Navalha',      1, 'Mensal',     'Pix',               NOW() - INTERVAL '70 days',  'Ativo'),
    ('Coworking Hub Sul',      3, 'Trimestral', 'Boleto',            NOW() - INTERVAL '95 days',  'Ativo'),
    ('Floricultura Primavera', 1, 'Anual',      'Pix',               NOW() - INTERVAL '200 days', 'Ativo'),
    ('Agência Pixel',          2, 'Mensal',     'Cartão de Crédito', NOW() - INTERVAL '15 days',  'Ativo');

-- Histórico de cobranças: um registro por ciclo, do início da assinatura até hoje.
-- Valores seguem a regra de ciclos: mensal 1x, trimestral 3x, semestral 5x (1 mês grátis) e anual 10x (2 meses grátis).
-- Ficam vencidos os ciclos listados em "Vencidas" (índice do ciclo, começando em 0), um em cada faixa de atraso;
-- os demais foram pagos. Os próximos ciclos são gerados pela própria API (CobrancaWorker).
WITH ciclos AS (
    SELECT a.Id, a.FormaPagamento, a.Status, a.DataInicio::date AS Inicio, pl.Valor AS ValorMensal,
           CASE a.TipoRecorrencia WHEN 'Mensal' THEN 1 WHEN 'Trimestral' THEN 3 WHEN 'Semestral' THEN 6 ELSE 12 END AS Meses,
           CASE a.TipoRecorrencia WHEN 'Mensal' THEN 1 WHEN 'Trimestral' THEN 3 WHEN 'Semestral' THEN 5 ELSE 10 END AS MesesCobrados,
           COALESCE(v.Vencidas, '{}') AS Vencidas
    FROM Assinaturas a
    JOIN Planos pl ON pl.Id = a.PlanoId
    LEFT JOIN (VALUES
        ('Livraria Página 2',   ARRAY[0]),   -- 25 dias  (1 a 30)
        ('Coworking Hub Sul',   ARRAY[1]),   -- ~5 dias  (1 a 30)
        ('Clínica Bem Viver',   ARRAY[0]),   -- 45 dias  (31 a 60)
        ('Academia Ritmo',      ARRAY[0]),   -- 80 dias  (61 a 90)
        ('Pet Shop Amigo Fiel', ARRAY[0])    -- 120 dias (mais de 90)
    ) AS v (ClienteNome, Vencidas) ON v.ClienteNome = a.ClienteNome
),
periodos AS (
    SELECT c.*, k,
           (c.Inicio + (k * c.Meses || ' months')::interval)::date       AS PeriodoInicio,
           (c.Inicio + ((k + 1) * c.Meses || ' months')::interval)::date AS PeriodoFim
    FROM ciclos c
    CROSS JOIN generate_series(0, 24) AS k
)
INSERT INTO Cobrancas (AssinaturaId, PeriodoInicio, PeriodoFim, DataVencimento, Valor, Status, DataPagamento, FormaPagamento)
SELECT Id, PeriodoInicio, PeriodoFim, PeriodoInicio,
       ROUND(ValorMensal * MesesCobrados, 2),
       CASE WHEN k = ANY(Vencidas) THEN 'Vencida' ELSE 'Paga' END,
       CASE WHEN k = ANY(Vencidas) THEN NULL
            ELSE LEAST(PeriodoInicio + (k % 3) * INTERVAL '1 day' + INTERVAL '10 hours', NOW()) END,
       CASE WHEN k = ANY(Vencidas) THEN NULL ELSE FormaPagamento END
FROM periodos
WHERE PeriodoInicio <= CURRENT_DATE
  AND (Status = 'Ativo' OR k = 0);   -- a assinatura inativa parou de gerar ciclos depois do primeiro

UPDATE Assinaturas a
SET DataExpiracao = (SELECT MAX(c.PeriodoFim) FROM Cobrancas c WHERE c.AssinaturaId = a.Id);

UPDATE Assinaturas SET ClienteEmail = v.Email
FROM (VALUES
    ('Padaria Pão Quente',     'financeiro@paoquente.example'),
    ('Studio Fit',             'contato@studiofit.example'),
    ('Clínica Bem Viver',      'adm@bemviver.example'),
    ('Livraria Página 2',      'livraria@pagina2.example'),
    ('Escola de Idiomas Go',   'secretaria@idiomasgo.example'),
    ('Café Grão Nobre',        'cafe@graonobre.example'),
    ('Academia Ritmo',         'financeiro@academiaritmo.example'),
    ('Barbearia Navalha',      'contato@navalha.example'),
    ('Coworking Hub Sul',      'admin@hubsul.example'),
    ('Floricultura Primavera', 'flores@primavera.example'),
    ('Agência Pixel',          'ola@agenciapixel.example')
    -- Pet Shop Amigo Fiel fica sem e-mail de propósito, para mostrar o caso "cliente sem e-mail"
) AS v (ClienteNome, Email)
WHERE Assinaturas.ClienteNome = v.ClienteNome;
