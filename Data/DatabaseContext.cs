using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ControleEnvios.Data
{
    public static class DatabaseContext
    {
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "controle_envios.db");
        public static string ConnectionString => $"Data Source={DbPath}";

        public static SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public static void InitializeDatabase()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS fornecedores (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    nome TEXT NOT NULL,
                    apelido TEXT,
                    cnpj_cpf TEXT,
                    cep TEXT,
                    logradouro TEXT,
                    numero TEXT,
                    complemento TEXT,
                    bairro TEXT,
                    cidade TEXT,
                    uf TEXT,
                    telefone TEXT,
                    email TEXT,
                    observacoes TEXT,
                    ativo INTEGER DEFAULT 1,
                    created_at TEXT
                );

                CREATE TABLE IF NOT EXISTS envios (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    numero_pedido TEXT NOT NULL,
                    cliente TEXT NOT NULL,
                    cliente_cpf TEXT,
                    cliente_celular TEXT,
                    cliente_email TEXT,
                    endereco_entrega TEXT,
                    complemento_entrega TEXT,
                    cidade_destino TEXT,
                    uf_destino TEXT,
                    cep_destino TEXT,
                    forma_envio_loja TEXT,
                    peso_gramas REAL DEFAULT 0,
                    valor_pedido REAL DEFAULT 0,
                    valor_frete_loja REAL DEFAULT 0,
                    
                    origem_tipo TEXT DEFAULT 'PROPRIO',
                    fornecedor_id INTEGER,
                    
                    nota_fiscal TEXT,
                    codigo_rastreio TEXT,
                    tipo_rastreio TEXT DEFAULT 'NORMAL',
                    data_envio TEXT,
                    valor_correios REAL DEFAULT 0,
                    
                    status_correios TEXT DEFAULT 'Pendente',
                    descricao_status TEXT,
                    ultimo_evento_data TEXT,
                    ultimo_evento_local TEXT,
                    entregue INTEGER DEFAULT 0,
                    
                    observacoes TEXT,
                    caminho_pdf TEXT,
                    created_at TEXT,
                    updated_at TEXT,
                    FOREIGN KEY (fornecedor_id) REFERENCES fornecedores (id)
                );

                CREATE TABLE IF NOT EXISTS itens_envio (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    envio_id INTEGER NOT NULL,
                    nome_produto TEXT NOT NULL,
                    referencia TEXT,
                    codigo TEXT,
                    tamanho TEXT,
                    quantidade INTEGER DEFAULT 1,
                    valor_unitario REAL DEFAULT 0,
                    valor_total REAL DEFAULT 0,
                    FOREIGN KEY (envio_id) REFERENCES envios (id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS historico_rastreio (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    envio_id INTEGER NOT NULL,
                    codigo_rastreio TEXT NOT NULL,
                    data_hora TEXT NOT NULL,
                    descricao TEXT NOT NULL,
                    detalhe TEXT,
                    unidade_local TEXT,
                    unidade_destino TEXT,
                    tipo_evento TEXT,
                    FOREIGN KEY (envio_id) REFERENCES envios (id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS configuracoes (
                    chave TEXT PRIMARY KEY,
                    valor TEXT
                );

                CREATE TABLE IF NOT EXISTS emails_coleta_processados (
                    message_id TEXT PRIMARY KEY,
                    remetente TEXT,
                    assunto TEXT,
                    data_recebimento TEXT,
                    pedidos_atualizados INTEGER DEFAULT 0,
                    processado_em TEXT
                );
            ";
            cmd.ExecuteNonQuery();

            // Registra suporte a encodings legados (windows-1252, etc)
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            }
            catch { }

            // Migrações seguras de colunas em tabelas existentes
            try
            {
                using var cmdMig = conn.CreateCommand();
                cmdMig.CommandText = "ALTER TABLE envios ADD COLUMN caminho_pdf TEXT;";
                cmdMig.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig2 = conn.CreateCommand();
                cmdMig2.CommandText = "ALTER TABLE envios ADD COLUMN complemento_entrega TEXT;";
                cmdMig2.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig3 = conn.CreateCommand();
                cmdMig3.CommandText = "ALTER TABLE envios ADD COLUMN desativar_avisos INTEGER DEFAULT 0;";
                cmdMig3.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig4 = conn.CreateCommand();
                cmdMig4.CommandText = "ALTER TABLE envios ADD COLUMN arquivado INTEGER DEFAULT 0;";
                cmdMig4.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig5 = conn.CreateCommand();
                cmdMig5.CommandText = "ALTER TABLE envios ADD COLUMN data_entrega TEXT;";
                cmdMig5.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig6 = conn.CreateCommand();
                cmdMig6.CommandText = "ALTER TABLE envios ADD COLUMN data_arquivamento TEXT;";
                cmdMig6.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig7 = conn.CreateCommand();
                cmdMig7.CommandText = "ALTER TABLE envios ADD COLUMN protocolo_reclamacao TEXT;";
                cmdMig7.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig8 = conn.CreateCommand();
                cmdMig8.CommandText = "ALTER TABLE envios ADD COLUMN data_reclamacao TEXT;";
                cmdMig8.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig9 = conn.CreateCommand();
                cmdMig9.CommandText = "ALTER TABLE envios ADD COLUMN status_reclamacao TEXT;";
                cmdMig9.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig10 = conn.CreateCommand();
                cmdMig10.CommandText = "ALTER TABLE envios ADD COLUMN codigo_autorizacao_reversa TEXT;";
                cmdMig10.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig11 = conn.CreateCommand();
                cmdMig11.CommandText = "ALTER TABLE envios ADD COLUMN data_limite_reversa TEXT;";
                cmdMig11.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            try
            {
                using var cmdMig12 = conn.CreateCommand();
                cmdMig12.CommandText = "ALTER TABLE envios ADD COLUMN status_reversa TEXT;";
                cmdMig12.ExecuteNonQuery();
            }
            catch { /* Coluna já existe */ }

            // Insere configurações padrão se não existirem
            InserirConfigPadrao(conn, "correios_usuario", "");
            InserirConfigPadrao(conn, "correios_codigo_acesso", "");
            InserirConfigPadrao(conn, "correios_cartao_postagem", "");
            InserirConfigPadrao(conn, "correios_ambiente", "PRODUCAO");
            InserirConfigPadrao(conn, "intervalo_minutos_verificacao", "60");
            InserirConfigPadrao(conn, "notificar_desktop", "1");
            InserirConfigPadrao(conn, "minimizar_para_tray", "1");

            // Configurações Leitura de E-mail Correios Coleta
            InserirConfigPadrao(conn, "email_host", "imap.gmail.com");
            InserirConfigPadrao(conn, "email_porta", "993");
            InserirConfigPadrao(conn, "email_usuario", "");
            InserirConfigPadrao(conn, "email_senha", "");
            InserirConfigPadrao(conn, "email_remetente_filtro", "naorespondacorreios@gmail.com");

            // Configurações API Nota Fiscal (preparação futura)
            InserirConfigPadrao(conn, "nfe_api_url", "");
            InserirConfigPadrao(conn, "nfe_api_token", "");

            // Configuração de arquivamento automático após 7 dias de entregue
            InserirConfigPadrao(conn, "arquivar_entregues_7_dias", "1");
        }

        private static void InserirConfigPadrao(SqliteConnection conn, string chave, string valor)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO configuracoes (chave, valor) VALUES (@c, @v)";
            cmd.Parameters.AddWithValue("@c", chave);
            cmd.Parameters.AddWithValue("@v", valor);
            cmd.ExecuteNonQuery();
        }

        public static string ObterConfig(string chave, string padrao = "")
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT valor FROM configuracoes WHERE chave = @c";
            cmd.Parameters.AddWithValue("@c", chave);
            var res = cmd.ExecuteScalar();
            return res != null && res != DBNull.Value ? res.ToString()! : padrao;
        }

        public static void SalvarConfig(string chave, string valor)
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO configuracoes (chave, valor) VALUES (@c, @v) ON CONFLICT(chave) DO UPDATE SET valor = @v";
            cmd.Parameters.AddWithValue("@c", chave);
            cmd.Parameters.AddWithValue("@v", valor);
            cmd.ExecuteNonQuery();
        }

        public static bool EmailJaProcessado(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId)) return false;

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM emails_coleta_processados WHERE message_id = @id";
            cmd.Parameters.AddWithValue("@id", messageId.Trim());
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }

        public static void RegistrarEmailProcessado(string messageId, string remetente, string assunto, string dataRecebimento, int atualizados)
        {
            if (string.IsNullOrWhiteSpace(messageId)) return;

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO emails_coleta_processados (
                    message_id, remetente, assunto, data_recebimento, pedidos_atualizados, processado_em
                ) VALUES (
                    @id, @rem, @ass, @dt, @qtd, @proc
                )";
            cmd.Parameters.AddWithValue("@id", messageId.Trim());
            cmd.Parameters.AddWithValue("@rem", remetente ?? "");
            cmd.Parameters.AddWithValue("@ass", assunto ?? "");
            cmd.Parameters.AddWithValue("@dt", dataRecebimento ?? "");
            cmd.Parameters.AddWithValue("@qtd", atualizados);
            cmd.Parameters.AddWithValue("@proc", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }
    }
}
