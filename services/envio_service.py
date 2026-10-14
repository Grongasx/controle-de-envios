import sqlite3
import os
from datetime import datetime
from typing import Dict, Any, List, Optional
import pandas as pd

from database import get_connection

class EnvioService:
    """
    Camada de serviço para regras de negócio de Envios, Fornecedores e Rastreios.
    """

    # --- FORNECEDORES ---

    @staticmethod
    def listar_fornecedores(somente_ativos: bool = True) -> List[Dict[str, Any]]:
        conn = get_connection()
        cursor = conn.cursor()
        query = "SELECT * FROM fornecedores WHERE 1=1"
        if somente_ativos:
            query += " AND ativo = 1"
        query += " ORDER BY nome ASC"
        cursor.execute(query)
        rows = [dict(r) for r in cursor.fetchall()]
        conn.close()
        return rows

    @staticmethod
    def salvar_fornecedor(dados: Dict[str, Any]) -> int:
        conn = get_connection()
        cursor = conn.cursor()
        f_id = dados.get("id")
        
        if f_id:
            cursor.execute("""
                UPDATE fornecedores SET
                    nome = ?, apelido = ?, cnpj_cpf = ?, cep = ?,
                    logradouro = ?, numero = ?, complemento = ?,
                    bairro = ?, cidade = ?, uf = ?, telefone = ?,
                    email = ?, observacoes = ?, ativo = ?
                WHERE id = ?
            """, (
                dados.get("nome"), dados.get("apelido"), dados.get("cnpj_cpf"), dados.get("cep"),
                dados.get("logradouro"), dados.get("numero"), dados.get("complemento"),
                dados.get("bairro"), dados.get("cidade"), dados.get("uf"), dados.get("telefone"),
                dados.get("email"), dados.get("observacoes"), dados.get("ativo", 1), f_id
            ))
            novo_id = f_id
        else:
            cursor.execute("""
                INSERT INTO fornecedores (
                    nome, apelido, cnpj_cpf, cep, logradouro, numero, complemento,
                    bairro, cidade, uf, telefone, email, observacoes, ativo, created_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                dados.get("nome"), dados.get("apelido"), dados.get("cnpj_cpf"), dados.get("cep"),
                dados.get("logradouro"), dados.get("numero"), dados.get("complemento"),
                dados.get("bairro"), dados.get("cidade"), dados.get("uf"), dados.get("telefone"),
                dados.get("email"), dados.get("observacoes"), dados.get("ativo", 1),
                datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            ))
            novo_id = cursor.lastrowid
            
        conn.commit()
        conn.close()
        return novo_id

    @staticmethod
    def excluir_fornecedor(f_id: int):
        conn = get_connection()
        cursor = conn.cursor()
        # Soft delete
        cursor.execute("UPDATE fornecedores SET ativo = 0 WHERE id = ?", (f_id,))
        conn.commit()
        conn.close()

    # --- ENVIOS ---

    @staticmethod
    def listar_envios(
        busca: str = "",
        status_filtro: str = "TODOS",
        origem_filtro: str = "TODAS"
    ) -> List[Dict[str, Any]]:
        conn = get_connection()
        cursor = conn.cursor()
        
        query = """
            SELECT 
                e.*,
                f.nome as fornecedor_nome,
                f.apelido as fornecedor_apelido,
                (SELECT GROUP_CONCAT(i.quantidade || 'x ' || i.nome_produto, '; ') 
                 FROM itens_envio i WHERE i.envio_id = e.id) as resumo_produtos
            FROM envios e
            LEFT JOIN fornecedores f ON e.fornecedor_id = f.id
            WHERE 1=1
        """
        params = []
        
        if busca.strip():
            b = f"%{busca.strip()}%"
            query += """ AND (
                e.numero_pedido LIKE ? OR 
                e.cliente LIKE ? OR 
                e.codigo_rastreio LIKE ? OR 
                e.nota_fiscal LIKE ?
            )"""
            params.extend([b, b, b, b])
            
        if status_filtro != "TODOS":
            if status_filtro == "PENDENTES_ENTREGA":
                query += " AND e.entregue = 0"
            elif status_filtro == "ENTREGUES":
                query += " AND e.entregue = 1"
            else:
                query += " AND e.status_correios = ?"
                params.append(status_filtro)
                
        if origem_filtro != "TODAS":
            if origem_filtro == "PROPRIO":
                query += " AND e.origem_tipo = 'PROPRIO'"
            elif origem_filtro == "DROPSHIPPING":
                query += " AND e.origem_tipo = 'DROPSHIPPING'"
            elif origem_filtro.startswith("FORNECEDOR_"):
                f_id = int(origem_filtro.split("_")[1])
                query += " AND e.fornecedor_id = ?"
                params.append(f_id)
                
        query += " ORDER BY e.id DESC"
        
        cursor.execute(query, params)
        rows = [dict(r) for r in cursor.fetchall()]
        conn.close()
        return rows

    @staticmethod
    def obter_envio_completo(envio_id: int) -> Optional[Dict[str, Any]]:
        conn = get_connection()
        cursor = conn.cursor()
        
        cursor.execute("""
            SELECT e.*, f.nome as fornecedor_nome, f.apelido as fornecedor_apelido,
                   f.logradouro as forn_logradouro, f.numero as forn_numero,
                   f.bairro as forn_bairro, f.cidade as forn_cidade, f.uf as forn_uf, f.cep as forn_cep
            FROM envios e
            LEFT JOIN fornecedores f ON e.fornecedor_id = f.id
            WHERE e.id = ?
        """, (envio_id,))
        envio_row = cursor.fetchone()
        if not envio_row:
            conn.close()
            return None
            
        envio = dict(envio_row)
        
        cursor.execute("SELECT * FROM itens_envio WHERE envio_id = ?", (envio_id,))
        envio["itens"] = [dict(r) for r in cursor.fetchall()]
        
        cursor.execute("SELECT * FROM historico_rastreio WHERE envio_id = ? ORDER BY id DESC", (envio_id,))
        envio["historico"] = [dict(r) for r in cursor.fetchall()]
        
        conn.close()
        return envio

    @staticmethod
    def salvar_envio(dados: Dict[str, Any], itens: List[Dict[str, Any]]) -> int:
        conn = get_connection()
        cursor = conn.cursor()
        envio_id = dados.get("id")
        agora = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        
        # Origem
        origem_tipo = dados.get("origem_tipo", "PROPRIO")
        fornecedor_id = dados.get("fornecedor_id") if origem_tipo == "DROPSHIPPING" else None

        if envio_id:
            cursor.execute("""
                UPDATE envios SET
                    numero_pedido = ?, cliente = ?, cliente_cpf = ?, cliente_celular = ?, cliente_email = ?,
                    endereco_entrega = ?, cidade_destino = ?, uf_destino = ?, cep_destino = ?,
                    forma_envio_loja = ?, peso_gramas = ?, valor_pedido = ?, valor_frete_loja = ?,
                    origem_tipo = ?, fornecedor_id = ?, nota_fiscal = ?, codigo_rastreio = ?,
                    tipo_rastreio = ?, data_envio = ?, valor_correios = ?, observacoes = ?, updated_at = ?
                WHERE id = ?
            """, (
                dados.get("numero_pedido"), dados.get("cliente"), dados.get("cliente_cpf"),
                dados.get("cliente_celular"), dados.get("cliente_email"), dados.get("endereco_entrega"),
                dados.get("cidade_destino"), dados.get("uf_destino"), dados.get("cep_destino"),
                dados.get("forma_envio_loja"), dados.get("peso_gramas", 0), dados.get("valor_pedido", 0),
                dados.get("valor_frete_loja", 0), origem_tipo, fornecedor_id, dados.get("nota_fiscal"),
                dados.get("codigo_rastreio", "").strip().upper(), dados.get("tipo_rastreio", "NORMAL"),
                dados.get("data_envio"), dados.get("valor_correios", 0), dados.get("observacoes"),
                agora, envio_id
            ))
            cursor.execute("DELETE FROM itens_envio WHERE envio_id = ?", (envio_id,))
            final_id = envio_id
        else:
            cursor.execute("""
                INSERT INTO envios (
                    numero_pedido, cliente, cliente_cpf, cliente_celular, cliente_email,
                    endereco_entrega, cidade_destino, uf_destino, cep_destino,
                    forma_envio_loja, peso_gramas, valor_pedido, valor_frete_loja,
                    origem_tipo, fornecedor_id, nota_fiscal, codigo_rastreio,
                    tipo_rastreio, data_envio, valor_correios, status_correios,
                    observacoes, created_at, updated_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                dados.get("numero_pedido"), dados.get("cliente"), dados.get("cliente_cpf"),
                dados.get("cliente_celular"), dados.get("cliente_email"), dados.get("endereco_entrega"),
                dados.get("cidade_destino"), dados.get("uf_destino"), dados.get("cep_destino"),
                dados.get("forma_envio_loja"), dados.get("peso_gramas", 0), dados.get("valor_pedido", 0),
                dados.get("valor_frete_loja", 0), origem_tipo, fornecedor_id, dados.get("nota_fiscal"),
                dados.get("codigo_rastreio", "").strip().upper(), dados.get("tipo_rastreio", "NORMAL"),
                dados.get("data_envio"), dados.get("valor_correios", 0),
                "Pré-postagem" if dados.get("tipo_rastreio") == "PRE_POSTAGEM" else "Pendente",
                dados.get("observacoes"), agora, agora
            ))
            final_id = cursor.lastrowid
            
        # Insere Itens
        for it in itens:
            cursor.execute("""
                INSERT INTO itens_envio (
                    envio_id, nome_produto, referencia, codigo, tamanho,
                    quantidade, valor_unitario, valor_total
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                final_id, it.get("nome_produto"), it.get("referencia"), it.get("codigo"),
                it.get("tamanho"), it.get("quantidade", 1), it.get("valor_unitario", 0),
                it.get("valor_total", 0)
            ))
            
        conn.commit()
        conn.close()
        return final_id

    @staticmethod
    def excluir_envio(envio_id: int):
        conn = get_connection()
        cursor = conn.cursor()
        cursor.execute("DELETE FROM itens_envio WHERE envio_id = ?", (envio_id,))
        cursor.execute("DELETE FROM historico_rastreio WHERE envio_id = ?", (envio_id,))
        cursor.execute("DELETE FROM envios WHERE id = ?", (envio_id,))
        conn.commit()
        conn.close()

    @staticmethod
    def atualizar_rastreio(envio_id: int, res: Dict[str, Any]) -> bool:
        """
        Atualiza o status e histórico de rastreio de um envio.
        Retorna True se houve mudança relevante de status.
        """
        if not res.get("sucesso"):
            return False

        conn = get_connection()
        cursor = conn.cursor()
        
        cursor.execute("SELECT status_correios FROM envios WHERE id = ?", (envio_id,))
        row = cursor.fetchone()
        if not row:
            conn.close()
            return False
            
        status_anterior = row[0]
        novo_status = res.get("status", "Pendente")
        descricao = res.get("descricao", "")
        ultimo_dt = res.get("ultimo_evento_data", "")
        ultimo_loc = res.get("ultimo_evento_local", "")
        entregue = 1 if res.get("entregue") else 0
        agora = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

        cursor.execute("""
            UPDATE envios SET
                status_correios = ?,
                descricao_status = ?,
                ultimo_evento_data = ?,
                ultimo_evento_local = ?,
                entregue = ?,
                updated_at = ?
            WHERE id = ?
        """, (novo_status, descricao, ultimo_dt, ultimo_loc, entregue, agora, envio_id))

        # Atualiza histórico se houver eventos
        eventos = res.get("eventos", [])
        if eventos:
            cursor.execute("DELETE FROM historico_rastreio WHERE envio_id = ?", (envio_id,))
            for ev in eventos:
                cursor.execute("""
                    INSERT INTO historico_rastreio (
                        envio_id, codigo_rastreio, data_hora, descricao,
                        detalhe, unidade_local, unidade_destino, tipo_evento
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """, (
                    envio_id, res.get("codigo"), ev.get("data_hora"), ev.get("descricao"),
                    ev.get("detalhe"), ev.get("local"), ev.get("destino"), ev.get("codigo")
                ))

        conn.commit()
        conn.close()

        # Retorna se mudou de status
        return status_anterior != novo_status

    @staticmethod
    def obter_metricas() -> Dict[str, int]:
        conn = get_connection()
        cursor = conn.cursor()
        
        cursor.execute("SELECT COUNT(*) FROM envios")
        total = cursor.fetchone()[0]
        
        cursor.execute("SELECT COUNT(*) FROM envios WHERE entregue = 1")
        entregues = cursor.fetchone()[0]
        
        cursor.execute("SELECT COUNT(*) FROM envios WHERE entregue = 0 AND status_correios = 'Em Trânsito'")
        em_transito = cursor.fetchone()[0]
        
        cursor.execute("SELECT COUNT(*) FROM envios WHERE status_correios = 'Saiu para Entrega'")
        saiu_entrega = cursor.fetchone()[0]
        
        cursor.execute("SELECT COUNT(*) FROM envios WHERE status_correios = 'Aguardando Retirada'")
        aguardando_retirada = cursor.fetchone()[0]
        
        cursor.execute("SELECT COUNT(*) FROM envios WHERE status_correios = 'Alerta / Retido'")
        alertas = cursor.fetchone()[0]
        
        cursor.execute("SELECT COUNT(*) FROM envios WHERE tipo_rastreio = 'PRE_POSTAGEM' AND status_correios IN ('Pendente', 'Pré-postagem')")
        pre_postagem = cursor.fetchone()[0]
        
        conn.close()
        return {
            "total": total,
            "entregues": entregues,
            "em_transito": em_transito,
            "saiu_entrega": saiu_entrega,
            "aguardando_retirada": aguardando_retirada,
            "alertas": alertas,
            "pre_postagem": pre_postagem
        }

    @staticmethod
    def exportar_para_excel(caminho_arquivo: str):
        conn = get_connection()
        query = """
            SELECT 
                e.numero_pedido as "Nº do Pedido",
                e.cliente as "Cliente",
                (SELECT GROUP_CONCAT(i.quantidade || 'x ' || i.nome_produto, '; ') 
                 FROM itens_envio i WHERE i.envio_id = e.id) as "Produtos",
                CASE 
                    WHEN e.origem_tipo = 'PROPRIO' THEN 'Estoque Próprio'
                    ELSE COALESCE(f.apelido, f.nome, 'Dropshipping')
                END as "Origem do Produto",
                e.nota_fiscal as "Nota Fiscal",
                e.codigo_rastreio as "Código de Rastreio",
                e.tipo_rastreio as "Tipo de Rastreio",
                e.status_correios as "Status Correios",
                e.ultimo_evento_data as "Última Atualização",
                e.data_envio as "Data de Envio",
                e.valor_correios as "Valor Correios",
                e.observacoes as "Observações"
            FROM envios e
            LEFT JOIN fornecedores f ON e.fornecedor_id = f.id
            ORDER BY e.id DESC
        """
        df = pd.read_sql_query(query, conn)
        conn.close()
        df.to_excel(caminho_arquivo, index=False)
