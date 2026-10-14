import re
import os
from typing import Dict, Any, List
import pdfplumber

class TrayPDFExtractor:
    """
    Extrator especializado em ordens de serviço e impressões de pedidos da plataforma Tray.
    Extrai número do pedido, dados do cliente, itens comprados, endereço e detalhes de envio.
    """
    
    @classmethod
    def extrair_dados_pdf(cls, pdf_path: str) -> Dict[str, Any]:
        if not os.path.exists(pdf_path):
            raise FileNotFoundError(f"Arquivo não encontrado: {pdf_path}")
            
        dados = {
            "numero_pedido": "",
            "data_pedido": "",
            "cliente": "",
            "cliente_cpf": "",
            "cliente_celular": "",
            "cliente_email": "",
            "endereco_entrega": "",
            "cidade_destino": "",
            "uf_destino": "",
            "cep_destino": "",
            "forma_envio_loja": "",
            "peso_gramas": 0.0,
            "valor_pedido": 0.0,
            "valor_frete_loja": 0.0,
            "itens": []
        }
        
        texto_completo = ""
        todas_tabelas = []
        
        with pdfplumber.open(pdf_path) as pdf:
            for pagina in pdf.pages:
                texto = pagina.extract_text(layout=False) or ""
                texto_completo += "\n" + texto
                
                # Extrai tabelas se existirem na página
                tabelas = pagina.extract_tables()
                if tabelas:
                    todas_tabelas.extend(tabelas)

        # 1. Número do Pedido
        match_pedido = re.search(r"Pedido\s*#?(\d+)", texto_completo, re.IGNORECASE)
        if match_pedido:
            dados["numero_pedido"] = match_pedido.group(1).strip()
        else:
            # Fallback pela URL no rodapé: orders_id=1535
            match_url_id = re.search(r"orders_id=(\d+)", texto_completo)
            if match_url_id:
                dados["numero_pedido"] = match_url_id.group(1).strip()

        # 2. Data do Pedido
        match_data = re.search(r"(\d{1,2}\s+de\s+[a-zç]+\s+de\s+\d{4}(?:\s+às\s+\d{2}:\d{2}:\d{2})?)", texto_completo, re.IGNORECASE)
        if match_data:
            dados["data_pedido"] = match_data.group(1).strip()

        # 3. CPF do Cliente
        match_cpf = re.search(r"CPF:\s*([\d\.\-]+)", texto_completo, re.IGNORECASE)
        if match_cpf:
            dados["cliente_cpf"] = match_cpf.group(1).strip()

        # 4. Celular do Cliente
        match_cel = re.search(r"Celular:\s*([\(\)\d\s\-]+)", texto_completo, re.IGNORECASE)
        if match_cel:
            dados["cliente_celular"] = re.sub(r"\s+", " ", match_cel.group(1)).strip()

        # 5. E-mail do Cliente
        match_email = re.search(r"([\w\.\-]+@[\w\.\-]+\.[a-zA-Z]{2,})", texto_completo)
        if match_email:
            dados["cliente_email"] = match_email.group(1).strip()

        # 6. Nome do Cliente
        # Padrão A: bloco "Dados do cliente" seguido do nome
        match_cliente = re.search(r"Dados do cliente\s*\n+([A-Za-zÀ-ÖØ-öø-ÿ\s]+?)(?:\n\s*Celular:|\n\s*Telefone:|\n\s*E-?mail|\n\s*CPF)", texto_completo)
        if match_cliente:
            dados["cliente"] = " ".join(match_cliente.group(1).split())
        else:
            # Padrão B: Destinatário na página 2
            match_dest = re.search(r"DESTINATÁRIO\s*\n+(?:[A-Z\s]+\n+)?([A-ZÀ-ÖØ-öø-ÿ\s]{4,40})\n", texto_completo)
            if match_dest:
                cand = match_dest.group(1).strip()
                if cand not in ["SEDEX", "PAC", "REMETENTE"]:
                    dados["cliente"] = cand

        # 7. CEP e Endereço de Entrega
        match_cep = re.search(r"CEP:\s*(\d{5}\-?\d{3})", texto_completo, re.IGNORECASE)
        if match_cep:
            dados["cep_destino"] = match_cep.group(1).strip()
        else:
            match_cep2 = re.search(r"\b(\d{5}-\d{3})\b", texto_completo)
            if match_cep2:
                dados["cep_destino"] = match_cep2.group(1).strip()

        # Cidade e UF
        match_cidade_uf = re.search(r"([A-Za-zÀ-ÖØ-öø-ÿ\s]+)\s*-\s*([A-Z]{2})\s*-\s*Brasil", texto_completo)
        if match_cidade_uf:
            dados["cidade_destino"] = match_cidade_uf.group(1).strip()
            dados["uf_destino"] = match_cidade_uf.group(2).strip()

        # Bloco de Endereço de Entrega
        match_end = re.search(r"Endereço de entrega\s*\n(?:Aos cuidados:[^\n]+\n)?([\s\S]+?)(?:\n\s*Endereço de cobrança|\n\s*Produto|\n\s*Subtotal)", texto_completo)
        if match_end:
            endereco_linhas = [l.strip() for l in match_end.group(1).split("\n") if l.strip()]
            dados["endereco_entrega"] = ", ".join(endereco_linhas)

        # 8. Forma de Envio (Loja)
        match_envio = re.search(r"Forma de envio\s*\n+([A-Za-z0-9\s\-]+?)(?:\n\s*R\$|\n\s*Peso)", texto_completo)
        if match_envio:
            dados["forma_envio_loja"] = match_envio.group(1).strip()
        elif "SEDEX" in texto_completo:
            dados["forma_envio_loja"] = "SEDEX"
        elif "PAC" in texto_completo:
            dados["forma_envio_loja"] = "PAC"

        # 9. Peso do Pedido (gramas)
        match_peso = re.search(r"Peso do pedido:\s*([\d\.,]+)\s*gramas", texto_completo, re.IGNORECASE)
        if match_peso:
            try:
                p_str = match_peso.group(1).replace(".", "").replace(",", ".")
                dados["peso_gramas"] = float(p_str)
            except Exception:
                pass

        # 10. Valores (Frete e Total do Pedido)
        match_frete = re.search(r"Valor do frete\s*R\$\s*([\d\.,]+)", texto_completo, re.IGNORECASE)
        if match_frete:
            try:
                dados["valor_frete_loja"] = float(match_frete.group(1).replace(".", "").replace(",", "."))
            except Exception:
                pass

        match_total = re.search(r"Total do pedido\s*R\$\s*([\d\.,]+)", texto_completo, re.IGNORECASE)
        if match_total:
            try:
                dados["valor_pedido"] = float(match_total.group(1).replace(".", "").replace(",", "."))
            except Exception:
                pass

        # 11. Itens do Pedido (Produtos e Quantidades)
        dados["itens"] = cls._extrair_itens(texto_completo, todas_tabelas)
        
        return dados

    @classmethod
    def _extrair_itens(cls, texto: str, tabelas: List[List[List[str]]]) -> List[Dict[str, Any]]:
        itens = []
        
        # Tentativa 1: Via tabelas detectadas pelo pdfplumber
        for tabela in tabelas:
            if not tabela or len(tabela) < 2:
                continue
            header = [str(c or "").strip().lower() for c in tabela[0]]
            if any("produto" in h for h in header) and any("qtd" in h for h in header):
                idx_prod = -1
                idx_qtd = -1
                idx_total = -1
                for i, h in enumerate(header):
                    if "produto" in h:
                        idx_prod = i
                    elif "qtd" in h:
                        idx_qtd = i
                    elif "total" in h:
                        idx_total = i
                
                for row in tabela[1:]:
                    if not row or len(row) <= max(idx_prod, idx_qtd):
                        continue
                    prod_text = str(row[idx_prod] or "").strip()
                    qtd_text = str(row[idx_qtd] or "").strip()
                    total_text = str(row[idx_total] or "").strip() if idx_total != -1 and len(row) > idx_total else ""
                    
                    if not prod_text or "subtotal" in prod_text.lower():
                        continue
                        
                    # Extrai quantidade numérica
                    qtd_match = re.search(r"\b(\d+)\b", qtd_text)
                    qtd = int(qtd_match.group(1)) if qtd_match else 1
                    
                    # Extrai referência, código, tamanho se presentes
                    ref_m = re.search(r"\(Ref\.?\s*([^\)]+)\)", prod_text, re.IGNORECASE)
                    ref = ref_m.group(1).strip() if ref_m else ""
                    
                    cod_m = re.search(r"Código:\s*(\w+)", prod_text, re.IGNORECASE)
                    cod = cod_m.group(1).strip() if cod_m else ""
                    
                    tam_m = re.search(r"Tamanho:\s*([^\(\n]+)", prod_text, re.IGNORECASE)
                    tam = tam_m.group(1).strip() if tam_m else ""
                    
                    # Nome principal limpo (primeira linha antes de detalhes)
                    linhas = [l.strip() for l in prod_text.split("\n") if l.strip()]
                    nome_principal = linhas[0] if linhas else prod_text
                    
                    # Valor
                    val_unit = 0.0
                    val_tot = 0.0
                    tot_m = re.search(r"R\$\s*([\d\.,]+)", total_text)
                    if tot_m:
                        try:
                            val_tot = float(tot_m.group(1).replace(".", "").replace(",", "."))
                            val_unit = val_tot / qtd if qtd > 0 else val_tot
                        except Exception:
                            pass
                    
                    itens.append({
                        "nome_produto": nome_principal,
                        "descricao_completa": prod_text,
                        "referencia": ref,
                        "codigo": cod,
                        "tamanho": tam,
                        "quantidade": qtd,
                        "valor_unitario": val_unit,
                        "valor_total": val_tot
                    })

        # Tentativa 2: Fallback por Regex direto no texto se as tabelas vierem vazias
        if not itens:
            bloco_produtos = re.search(r"Produto\s+Qtd\.?\s+Total\s*\n([\s\S]+?)(?:\n\s*Subtotal|\n\s*Valor do frete)", texto)
            if bloco_produtos:
                conteudo = bloco_produtos.group(1).strip()
                # Procura padrões de produto
                match_p = re.finditer(r"([A-Z0-9\s°\-\(\)\.\/]+?)\n(?:\s*Tamanho:[^\n]+\n)?(?:\s*Código:[^\n]+\n)?\s*(\d+)\s*\n\s*R\$\s*([\d\.,]+)", conteudo)
                for m in match_p:
                    nome = m.group(1).strip()
                    qtd = int(m.group(2).strip())
                    val_tot = float(m.group(3).strip().replace(".", "").replace(",", "."))
                    itens.append({
                        "nome_produto": nome,
                        "referencia": "",
                        "codigo": "",
                        "tamanho": "",
                        "quantidade": qtd,
                        "valor_unitario": val_tot / qtd if qtd > 0 else val_tot,
                        "valor_total": val_tot
                    })

        return itens
