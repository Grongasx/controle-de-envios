import os
import sqlite3
from datetime import datetime

DB_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "controle_envios.db")

def get_connection():
    conn = sqlite3.connect(DB_FILE)
    conn.row_factory = sqlite3.Row
    return conn

def init_db():
    conn = get_connection()
    cursor = conn.cursor()
    
    # Tabela de Fornecedores (para Dropshipping e Remetentes)
    cursor.execute("""
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
    )
    """)
    
    # Tabela de Envios (Pedidos)
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS envios (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        numero_pedido TEXT NOT NULL,
        cliente TEXT NOT NULL,
        cliente_cpf TEXT,
        cliente_celular TEXT,
        cliente_email TEXT,
        endereco_entrega TEXT,
        cidade_destino TEXT,
        uf_destino TEXT,
        cep_destino TEXT,
        forma_envio_loja TEXT,
        peso_gramas REAL DEFAULT 0,
        valor_pedido REAL DEFAULT 0,
        valor_frete_loja REAL DEFAULT 0,
        
        origem_tipo TEXT DEFAULT 'PROPRIO', -- 'PROPRIO' ou 'DROPSHIPPING'
        fornecedor_id INTEGER,
        
        nota_fiscal TEXT,
        codigo_rastreio TEXT,
        tipo_rastreio TEXT DEFAULT 'NORMAL', -- 'NORMAL' ou 'PRE_POSTAGEM'
        data_envio TEXT,
        valor_correios REAL DEFAULT 0,
        
        status_correios TEXT DEFAULT 'Pendente',
        descricao_status TEXT,
        ultimo_evento_data TEXT,
        ultimo_evento_local TEXT,
        entregue INTEGER DEFAULT 0,
        
        observacoes TEXT,
        created_at TEXT,
        updated_at TEXT,
        FOREIGN KEY (fornecedor_id) REFERENCES fornecedores (id)
    )
    """)
    
    # Tabela de Itens do Pedido (1 para N)
    cursor.execute("""
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
    )
    """)
    
    # Tabela de Histórico de Rastreio (Timeline de Eventos da API dos Correios)
    cursor.execute("""
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
    )
    """)
    
    # Tabela de Configurações Gerais
    cursor.execute("""
    CREATE TABLE IF NOT EXISTS configuracoes (
        chave TEXT PRIMARY KEY,
        valor TEXT
    )
    """)
    
    # Configurações padrão iniciais
    configs_padrao = {
        "correios_usuario": "",
        "correios_codigo_acesso": "",
        "correios_cartao_postagem": "",
        "correios_ambiente": "PRODUCAO",
        "intervalo_minutos_verificacao": "60",
        "notificar_desktop": "1",
        "minimizar_para_tray": "1"
    }
    for chave, valor in configs_padrao.items():
        cursor.execute("INSERT OR IGNORE INTO configuracoes (chave, valor) VALUES (?, ?)", (chave, valor))
        
    conn.commit()
    conn.close()

if __name__ == "__main__":
    init_db()
    print("Banco de dados SQLite inicializado com sucesso!")
