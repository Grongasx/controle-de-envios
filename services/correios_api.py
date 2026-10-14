import base64
import requests
from datetime import datetime, timedelta
from typing import Dict, Any, List, Optional
import sqlite3
import os

DB_FILE = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "controle_envios.db")

class CorreiosAPIService:
    """
    Serviço de integração oficial com a API dos Correios (CWS - Correios Web Services).
    Gerencia autenticação por Token JWT, renovação automática, consulta SRO (Rastreamento)
    e status de Pré-postagens.
    """
    
    BASE_URL = "https://api.correios.com.br"
    
    _token_cache: Optional[str] = None
    _token_expira_em: Optional[datetime] = None

    @classmethod
    def _obter_credenciais(cls) -> Dict[str, str]:
        conn = sqlite3.connect(DB_FILE)
        cursor = conn.cursor()
        cursor.execute("SELECT chave, valor FROM configuracoes")
        rows = cursor.fetchall()
        conn.close()
        
        cfg = {r[0]: (r[1] or "").strip() for r in rows}
        return {
            "usuario": cfg.get("correios_usuario", ""),
            "codigo_acesso": cfg.get("correios_codigo_acesso", ""),
            "cartao_postagem": cfg.get("correios_cartao_postagem", ""),
            "ambiente": cfg.get("correios_ambiente", "PRODUCAO")
        }

    @classmethod
    def obter_token(cls, forcar_renovacao: bool = False) -> str:
        # Se token em cache ainda for válido (com 5 min de margem), reaproveita
        agora = datetime.now()
        if not forcar_renovacao and cls._token_cache and cls._token_expira_em and agora < (cls._token_expira_em - timedelta(minutes=5)):
            return cls._token_cache

        creds = cls._obter_credenciais()
        usuario = creds["usuario"]
        codigo_acesso = creds["codigo_acesso"]
        cartao = creds["cartao_postagem"]

        if not usuario or not codigo_acesso:
            raise ValueError("Credenciais dos Correios (Usuário e Código de Acesso) não configuradas.")

        auth_str = f"{usuario}:{codigo_acesso}"
        auth_base64 = base64.b64encode(auth_str.encode("utf-8")).decode("utf-8")

        headers = {
            "Authorization": f"Basic {auth_base64}",
            "Content-Type": "application/json",
            "Accept": "application/json"
        }

        # Se houver cartão de postagem, autentica no endpoint de cartão
        if cartao:
            url = f"{cls.BASE_URL}/token/v1/autentica/cartaopostagem"
            payload = {"numero": cartao}
            resp = requests.post(url, json=payload, headers=headers, timeout=15)
        else:
            url = f"{cls.BASE_URL}/token/v1/autentica"
            resp = requests.post(url, headers=headers, timeout=15)

        if resp.status_code not in (200, 201):
            raise Exception(f"Falha na autenticação dos Correios ({resp.status_code}): {resp.text}")

        dados_token = resp.json()
        token = dados_token.get("token")
        if not token:
            raise Exception("Token não retornado pela API dos Correios.")

        cls._token_cache = token
        expira_str = dados_token.get("expiraEm")
        if expira_str:
            try:
                # Trata formato ISO (ex: 2026-10-06T15:30:00-03:00)
                cls._token_expira_em = datetime.fromisoformat(expira_str.replace("Z", "+00:00"))
            except Exception:
                cls._token_expira_em = agora + timedelta(hours=6)
        else:
            cls._token_expira_em = agora + timedelta(hours=6)

        return token

    @classmethod
    def rastrear_objeto(cls, codigo: str) -> Dict[str, Any]:
        """
        Rastreia um único objeto nos Correios via SRO API.
        Retorna status consolidado, último evento e histórico completo.
        """
        codigo_limpo = codigo.strip().upper()
        if not codigo_limpo:
            return {"sucesso": False, "erro": "Código vazio"}

        token = cls.obter_token()
        url = f"{cls.BASE_URL}/srorastro/v1/objetos/{codigo_limpo}?resultado=T"
        headers = {
            "Authorization": f"Bearer {token}",
            "Accept": "application/json"
        }

        resp = requests.get(url, headers=headers, timeout=15)
        
        # Se token expirou (401), renova uma vez
        if resp.status_code == 401:
            token = cls.obter_token(forcar_renovacao=True)
            headers["Authorization"] = f"Bearer {token}"
            resp = requests.get(url, headers=headers, timeout=15)

        if resp.status_code != 200:
            return {
                "sucesso": False,
                "codigo": codigo_limpo,
                "erro": f"Erro {resp.status_code}: {resp.text}"
            }

        dados = resp.json()
        objetos = dados.get("objetos", [])
        if not objetos:
            return {
                "sucesso": True,
                "codigo": codigo_limpo,
                "status": "Não Encontrado",
                "descricao": "Objeto ainda não postado ou não consta na base dos Correios",
                "eventos": [],
                "entregue": False
            }

        obj = objetos[0]
        eventos = obj.get("eventos", [])
        
        if not eventos:
            msg = obj.get("mensagem", "Sem eventos registrados")
            return {
                "sucesso": True,
                "codigo": codigo_limpo,
                "status": "Aguardando Postagem",
                "descricao": msg,
                "eventos": [],
                "entregue": False
            }

        # O primeiro evento da lista é sempre o mais recente
        ultimo_evento = eventos[0]
        cod_evento = ultimo_evento.get("codigo", "")
        desc_evento = ultimo_evento.get("descricao", "")
        dt_hr = ultimo_evento.get("dtHrCriado", "")
        detalhe = ultimo_evento.get("detalhe", "")

        unidade = ultimo_evento.get("unidade", {})
        end_unidade = unidade.get("endereco", {})
        cidade_unidade = end_unidade.get("cidade", "")
        uf_unidade = end_unidade.get("uf", "")
        local_formatado = f"{cidade_unidade}/{uf_unidade}".strip("/") if (cidade_unidade or uf_unidade) else unidade.get("tipo", "")

        # Classificação de status unificado
        entregue = False
        if cod_evento in ("BDE", "BDI", "BDR") or "entregue" in desc_evento.lower():
            status_geral = "Entregue"
            entregue = True
        elif cod_evento == "OEC" or "saiu para entrega" in desc_evento.lower():
            status_geral = "Saiu para Entrega"
        elif cod_evento == "LDI" or "aguardando retirada" in desc_evento.lower():
            status_geral = "Aguardando Retirada"
        elif cod_evento in ("RO", "DO") or "em trânsito" in desc_evento.lower() or "encaminhado" in desc_evento.lower():
            status_geral = "Em Trânsito"
        elif cod_evento == "PO" or "postado" in desc_evento.lower():
            status_geral = "Postado"
        elif "devolvido" in desc_evento.lower() or "extraviado" in desc_evento.lower() or "problema" in desc_evento.lower():
            status_geral = "Alerta / Retido"
        else:
            status_geral = desc_evento or "Em Andamento"

        # Formata lista de eventos simplificada para exibição e gravação no banco
        eventos_formatados = []
        for ev in eventos:
            u = ev.get("unidade", {})
            e = u.get("endereco", {})
            cid = e.get("cidade", "")
            uf = e.get("uf", "")
            loc = f"{cid}/{uf}".strip("/") if (cid or uf) else u.get("tipo", "")
            
            dest = ev.get("unidadeDestino", {})
            dest_end = dest.get("endereco", {})
            dest_cid = dest_end.get("cidade", "")
            dest_uf = dest_end.get("uf", "")
            dest_loc = f"{dest_cid}/{dest_uf}".strip("/") if (dest_cid or dest_uf) else ""

            eventos_formatados.append({
                "data_hora": ev.get("dtHrCriado", ""),
                "codigo": ev.get("codigo", ""),
                "descricao": ev.get("descricao", ""),
                "detalhe": ev.get("detalhe", ""),
                "local": loc,
                "destino": dest_loc
            })

        return {
            "sucesso": True,
            "codigo": codigo_limpo,
            "status": status_geral,
            "descricao": desc_evento,
            "detalhe": detalhe,
            "ultimo_evento_data": dt_hr,
            "ultimo_evento_local": local_formatado,
            "entregue": entregue,
            "eventos": eventos_formatados
        }

    @classmethod
    def rastrear_lote(cls, codigos: List[str]) -> Dict[str, Dict[str, Any]]:
        """
        Rastreia múltiplos códigos em lote.
        """
        resultados = {}
        for cod in codigos:
            cod_limpo = cod.strip().upper()
            if cod_limpo:
                try:
                    resultados[cod_limpo] = cls.rastrear_objeto(cod_limpo)
                except Exception as e:
                    resultados[cod_limpo] = {
                        "sucesso": False,
                        "codigo": cod_limpo,
                        "erro": str(e)
                    }
        return resultados
