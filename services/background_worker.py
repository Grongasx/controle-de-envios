from PyQt6.QtCore import QThread, pyqtSignal
import time
from typing import Dict, Any, List
import sqlite3
import os

from services.correios_api import CorreiosAPIService
from services.envio_service import EnvioService
from database import get_connection

class BackgroundWorker(QThread):
    """
    Worker que roda em segundo plano consultando periodicamente
    a API dos Correios para encomendas pendentes.
    """
    
    progresso_sinal = pyqtSignal(str) # Mensagem de log/status
    atualizacao_concluida = pyqtSignal() # Sinal para o dashboard atualizar a tela
    notificacao_critica = pyqtSignal(str, str) # Título, Mensagem (para Windows Tray Notification)

    def __init__(self, parent=None):
        super().__init__(parent)
        self._executando = True
        self._forcar_verificacao = False

    def parar(self):
        self._executando = False

    def solicitar_atualizacao_imediata(self):
        self._forcar_verificacao = True

    def run(self):
        while self._executando:
            try:
                # Lê intervalo configurado no banco
                conn = get_connection()
                c = conn.cursor()
                c.execute("SELECT valor FROM configuracoes WHERE chave = 'intervalo_minutos_verificacao'")
                row = c.fetchone()
                intervalo_min = int(row[0]) if row and row[0] else 60
                conn.close()
            except Exception:
                intervalo_min = 60

            # Executa a verificação
            self._verificar_rastreios()

            # Aguarda o intervalo ou acionamento imediato
            segundos_espera = max(intervalo_min * 60, 60)
            contador = 0
            while contador < segundos_espera and self._executando:
                if self._forcar_verificacao:
                    self._forcar_verificacao = False
                    break
                time.sleep(1)
                contador += 1

    def _verificar_rastreios(self):
        try:
            conn = get_connection()
            c = conn.cursor()
            # Busca todos os envios não entregues com código de rastreio preenchido
            c.execute("""
                SELECT id, numero_pedido, cliente, codigo_rastreio, status_correios, tipo_rastreio
                FROM envios 
                WHERE entregue = 0 AND codigo_rastreio IS NOT NULL AND TRIM(codigo_rastreio) != ''
            """)
            pendentes = [dict(r) for r in c.fetchall()]
            conn.close()

            if not pendentes:
                return

            self.progresso_sinal.emit(f"Verificando {len(pendentes)} rastreios pendentes nos Correios...")

            for item in pendentes:
                if not self._executando:
                    break

                envio_id = item["id"]
                codigo = item["codigo_rastreio"]
                pedido = item["numero_pedido"]
                cliente = item["cliente"]
                status_antigo = item["status_correios"]

                try:
                    resultado = CorreiosAPIService.rastrear_objeto(codigo)
                    if resultado.get("sucesso"):
                        mudou = EnvioService.atualizar_rastreio(envio_id, resultado)
                        novo_status = resultado.get("status", "")

                        if mudou:
                            # Notificações críticas para chamar atenção
                            if novo_status == "Saiu para Entrega":
                                self.notificacao_critica.emit(
                                    f"Saiu para Entrega! (Pedido #{pedido})",
                                    f"O pacote do cliente {cliente} ({codigo}) saiu para entrega ao destinatário."
                                )
                            elif novo_status == "Aguardando Retirada":
                                self.notificacao_critica.emit(
                                    f"Aguardando Retirada! (Pedido #{pedido})",
                                    f"Atenção: Objeto de {cliente} ({codigo}) está aguardando retirada na agência!"
                                )
                            elif novo_status == "Entregue":
                                self.notificacao_critica.emit(
                                    f"Entregue com Sucesso! (Pedido #{pedido})",
                                    f"O pedido de {cliente} foi entregue com sucesso."
                                )
                            elif novo_status == "Alerta / Retido":
                                self.notificacao_critica.emit(
                                    f"Alerta nos Correios! (Pedido #{pedido})",
                                    f"Problema no envio de {cliente}: {resultado.get('descricao')}"
                                )
                except Exception as e:
                    # Loga mas segue para os próximos
                    pass

                # Pausa leve para respeitar taxa da API
                time.sleep(0.5)

            self.progresso_sinal.emit("Verificação de rastreios finalizada.")
            self.atualizacao_concluida.emit()

        except Exception as e:
            self.progresso_sinal.emit(f"Erro na verificação em background: {str(e)}")
