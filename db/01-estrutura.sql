-- =============================================================
-- Assina+ | 1/2 Estrutura do banco
-- Executado automaticamente pelo docker-compose na primeira subida (antes de 02-dados-demo.sql).
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

-- Quem paga. Um cliente pode ter várias assinaturas.
CREATE TABLE IF NOT EXISTS Clientes (
    Id            SERIAL PRIMARY KEY,
    Nome          VARCHAR(100) NOT NULL,
    Email         VARCHAR(150),            -- opcional; sem e-mail não há lembretes
    Documento     VARCHAR(14),             -- CPF ou CNPJ, só dígitos (opcional)
    Telefone      VARCHAR(20),
    DataCadastro  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_Clientes_Documento ON Clientes (Documento) WHERE Documento IS NOT NULL;

CREATE TABLE IF NOT EXISTS Assinaturas (
    Id               SERIAL PRIMARY KEY,
    ClienteId        INT          NOT NULL REFERENCES Clientes(Id),
    PlanoId          INT          NOT NULL REFERENCES Planos(Id),
    TipoRecorrencia  VARCHAR(20)  NOT NULL,   -- Mensal, Trimestral, Semestral, Anual
    FormaPagamento   VARCHAR(50)  NOT NULL,
    DataInicio       TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DataExpiracao    TIMESTAMP,
    Status           VARCHAR(20)  NOT NULL DEFAULT 'Ativo'  -- Ativo, Inativo
);

CREATE INDEX IF NOT EXISTS IX_Assinaturas_Cliente ON Assinaturas (ClienteId);

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

-- Controle do reset diário da demonstração (deploy público): um registro por dia em que os dados foram recriados
CREATE TABLE IF NOT EXISTS ResetsDemo (
    Dia           DATE      PRIMARY KEY,
    DataExecucao  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
