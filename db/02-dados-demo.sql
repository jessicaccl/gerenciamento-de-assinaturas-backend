-- =============================================================
-- Assina+ | 2/2 Dados de demonstração
-- Roda na primeira subida do banco e, no deploy público, todo dia pelo reset automático da API
-- (ServicoDeDadosDemo). Datas relativas a NOW() mantêm vencimentos e atrasos sempre atuais.
-- =============================================================

INSERT INTO Planos (Nome, Valor, Descricao, Ativo) VALUES
    ('Básico',       29.90,  'Ideal para começar: até 100 assinantes e relatórios mensais.', TRUE),
    ('Profissional', 79.90,  'Assinantes ilimitados, relatórios semanais e suporte por e-mail.', TRUE),
    ('Empresarial',  199.90, 'Tudo do Profissional, integrações via API e suporte prioritário.', TRUE);

-- Senhas geradas com bcrypt (compatível com BCrypt.Net usado pela API)
INSERT INTO Usuarios (Nome, Email, SenhaHash, Cargo) VALUES
    ('Conta Demo',     'demo@assinamais.dev',        crypt('demo123', gen_salt('bf', 11)), 'Admin'),
    ('Ana Colaboradora', 'colaborador@assinamais.dev', crypt('demo123', gen_salt('bf', 11)), 'Colaborador');

-- Clientes fictícios: e-mails no domínio reservado .example (RFC 2606), que nunca chegam a ninguém,
-- e CPF/CNPJ inventados, mas com dígitos verificadores válidos.
INSERT INTO Clientes (Nome, Email, Documento, Telefone) VALUES
    ('Padaria Pão Quente',     'financeiro@paoquente.example',      '12345678000195', '(11) 98765-4321'),
    ('Studio Fit',             'contato@studiofit.example',         '12345678909',    '(11) 96666-5555'),
    ('Clínica Bem Viver',      'adm@bemviver.example',              '34567890000130', '(31) 3333-4444'),
    ('Livraria Página 2',      'livraria@pagina2.example',          '23456789000195', '(21) 99876-5432'),
    ('Pet Shop Amigo Fiel',    NULL,                                '34567890175',    '(71) 90000-9999'),   -- sem e-mail de propósito
    ('Escola de Idiomas Go',   'secretaria@idiomasgo.example',      '67890123000116', '(61) 95555-4444'),
    ('Café Grão Nobre',        'cafe@graonobre.example',            '78901234000105', '(19) 94444-3333'),
    ('Academia Ritmo',         'financeiro@academiaritmo.example',  '45678901000175', '(41) 98888-7777'),
    ('Barbearia Navalha',      'contato@navalha.example',           '23456789092',    '(85) 93333-2222'),
    ('Coworking Hub Sul',      'admin@hubsul.example',              '56789012000100', '(51) 97777-6666'),
    ('Floricultura Primavera', 'flores@primavera.example',          '89012345000179', '(48) 92222-1111'),
    ('Agência Pixel',          'ola@agenciapixel.example',          '90123456000131', '(11) 91111-0000'),
    ('Mercado Bom Preço',      'compras@bompreco.example',          NULL,             '(11) 97000-1234');  -- recém-cadastrado, ainda sem assinatura

-- A Agência Pixel tem duas assinaturas, para mostrar um cliente com mais de um plano
INSERT INTO Assinaturas (ClienteId, PlanoId, TipoRecorrencia, FormaPagamento, DataInicio, Status)
SELECT cl.Id, v.PlanoId, v.Recorrencia, v.Pagamento, NOW() - (v.Dias || ' days')::interval, v.Status
FROM (VALUES
    ('Padaria Pão Quente',     1, 'Mensal',     'Pix',               20,  'Ativo'),
    ('Studio Fit',             2, 'Anual',      'Cartão de Crédito', 60,  'Ativo'),
    ('Clínica Bem Viver',      3, 'Semestral',  'Boleto',            45,  'Ativo'),
    ('Livraria Página 2',      2, 'Mensal',     'Cartão de Crédito', 25,  'Ativo'),
    ('Pet Shop Amigo Fiel',    1, 'Trimestral', 'Pix',               120, 'Inativo'),
    ('Escola de Idiomas Go',   3, 'Anual',      'Boleto',            10,  'Ativo'),
    ('Café Grão Nobre',        2, 'Mensal',     'Cartão de Crédito', 100, 'Ativo'),
    ('Academia Ritmo',         3, 'Mensal',     'Boleto',            80,  'Ativo'),
    ('Barbearia Navalha',      1, 'Mensal',     'Pix',               70,  'Ativo'),
    ('Coworking Hub Sul',      3, 'Trimestral', 'Boleto',            95,  'Ativo'),
    ('Floricultura Primavera', 1, 'Anual',      'Pix',               200, 'Ativo'),
    ('Agência Pixel',          2, 'Mensal',     'Cartão de Crédito', 15,  'Ativo'),
    ('Agência Pixel',          1, 'Mensal',     'Pix',               40,  'Ativo')
) AS v (ClienteNome, PlanoId, Recorrencia, Pagamento, Dias, Status)
JOIN Clientes cl ON cl.Nome = v.ClienteNome;

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
    JOIN Clientes cl ON cl.Id = a.ClienteId
    JOIN Planos pl ON pl.Id = a.PlanoId
    LEFT JOIN (VALUES
        ('Livraria Página 2',   ARRAY[0]),   -- 25 dias  (1 a 30)
        ('Coworking Hub Sul',   ARRAY[1]),   -- ~5 dias  (1 a 30)
        ('Clínica Bem Viver',   ARRAY[0]),   -- 45 dias  (31 a 60)
        ('Academia Ritmo',      ARRAY[0]),   -- 80 dias  (61 a 90)
        ('Pet Shop Amigo Fiel', ARRAY[0])    -- 120 dias (mais de 90)
    ) AS v (ClienteNome, Vencidas) ON v.ClienteNome = cl.Nome
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

-- O cliente "existe desde" a primeira assinatura
UPDATE Clientes cl
SET DataCadastro = (SELECT MIN(a.DataInicio) FROM Assinaturas a WHERE a.ClienteId = cl.Id)
WHERE EXISTS (SELECT 1 FROM Assinaturas a WHERE a.ClienteId = cl.Id);
