using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ControleEnvios.Data;
using ControleEnvios.Models;

namespace ControleEnvios.Services
{
    public class BackgroundMonitorService
    {
        private static BackgroundMonitorService? _instance;
        public static BackgroundMonitorService Instance => _instance ??= new BackgroundMonitorService();

        private CancellationTokenSource? _cts;
        private bool _isExecutando = false;

        public event Action<string, string>? NotificacaoCritica; // Titulo, Mensagem
        public event Action? AtualizacaoConcluida;
        public event Action<string>? LogProgresso;

        public void Iniciar()
        {
            if (_isExecutando) return;
            _isExecutando = true;
            _cts = new CancellationTokenSource();

            Task.Run(() => LoopMonitoramentoAsync(_cts.Token));
        }

        public void Parar()
        {
            _isExecutando = false;
            _cts?.Cancel();
        }

        public async Task VerificarAgoraAsync()
        {
            await ExecutarVerificacaoAsync();
        }

        private async Task LoopMonitoramentoAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _isExecutando)
            {
                try
                {
                    await ExecutarVerificacaoAsync();
                }
                catch (Exception ex)
                {
                    LogProgresso?.Invoke($"Erro no monitoramento: {ex.Message}");
                }

                int minutos = 60;
                try
                {
                    string minStr = DatabaseContext.ObterConfig("intervalo_minutos_verificacao", "60");
                    if (int.TryParse(minStr, out int m) && m > 0) minutos = m;
                }
                catch { }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(minutos), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ExecutarVerificacaoAsync()
        {
            // 1. Verifica e-mails dos Correios (comprovantes HTML de coleta) se configurado
            try
            {
                string emailUsuario = DatabaseContext.ObterConfig("email_usuario", "");
                string emailSenha = DatabaseContext.ObterConfig("email_senha", "");
                if (!string.IsNullOrWhiteSpace(emailUsuario) && !string.IsNullOrWhiteSpace(emailSenha))
                {
                    LogProgresso?.Invoke("Verificando e-mails de coleta dos Correios...");
                    var (atualizados, novos, novosItens, msg) = await CorreiosColetaReader.VerificarEmailsColetaAsync();
                    if (novos > 0)
                    {
                        string detalheItens = "";
                        if (novosItens != null && novosItens.Count > 0)
                        {
                            var amostra = novosItens.Take(2).Select(x => $"{x.CodigoRastreio} ({x.ValorCorreios:C2})");
                            detalheItens = " (" + string.Join(", ", amostra);
                            if (novosItens.Count > 2) detalheItens += $" +{novosItens.Count - 2}";
                            detalheItens += ")";
                        }

                        NotificacaoCritica?.Invoke(
                            "⚠️ Novo Envio Adicionado via Gmail (Cobrado)",
                            $"Adicionado(s) {novos} novo(s) envio(s) pela coleta do Gmail{detalheItens}.\n⚠️ ATENÇÃO: O envio foi cobrado pelos Correios, mas o PDF da Tray ainda não foi extraído!"
                        );
                        AtualizacaoConcluida?.Invoke();
                    }
                    else if (atualizados > 0)
                    {
                        NotificacaoCritica?.Invoke(
                            "📬 Coleta dos Correios Atualizada!",
                            $"{atualizados} pedido(s) atualizados com custo real dos Correios via Gmail."
                        );
                        AtualizacaoConcluida?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                LogProgresso?.Invoke($"Erro ao checar e-mails de coleta: {ex.Message}");
            }

            LogProgresso?.Invoke("Iniciando verificação de rastreios pendentes...");

            var pendentes = EnvioService.ListarEnvios(statusFiltro: "PENDENTES_ENTREGA");
            var listaParaVerificar = new List<Envio>();

            foreach (var env in pendentes)
            {
                if (!string.IsNullOrWhiteSpace(env.CodigoRastreio))
                {
                    listaParaVerificar.Add(env);
                }
            }

            if (listaParaVerificar.Count == 0)
            {
                LogProgresso?.Invoke("Nenhum envio pendente com código de rastreio para consultar.");
                return;
            }

            LogProgresso?.Invoke($"Consultando {listaParaVerificar.Count} encomendas na API dos Correios...");

            foreach (var envio in listaParaVerificar)
            {
                if (!_isExecutando) break;

                try
                {
                    var resultado = await CorreiosApiService.RastrearObjetoAsync(envio.CodigoRastreio);
                    if (resultado.Sucesso)
                    {
                        bool mudou = EnvioService.AtualizarRastreio(envio.Id, resultado);
                        if (mudou)
                        {
                            string st = resultado.Status;
                            if (st == "Saiu para Entrega")
                            {
                                NotificacaoCritica?.Invoke(
                                    $"Saiu para Entrega! (Pedido #{envio.NumeroPedido})",
                                    $"A encomenda de {envio.Cliente} ({envio.CodigoRastreio}) saiu para entrega ao destinatário."
                                );
                            }
                            else if (st == "Aguardando Retirada")
                            {
                                NotificacaoCritica?.Invoke(
                                    $"Aguardando Retirada! (Pedido #{envio.NumeroPedido})",
                                    $"Atenção: Objeto de {envio.Cliente} ({envio.CodigoRastreio}) aguarda retirada na agência!"
                                );
                            }
                            else if (st == "Entregue")
                            {
                                NotificacaoCritica?.Invoke(
                                    $"Entregue com Sucesso! (Pedido #{envio.NumeroPedido})",
                                    $"O pedido de {envio.Cliente} foi entregue com sucesso."
                                );
                            }
                            else if (st == "Carteiro Não Atendido" || st.Contains("Não Atendido", StringComparison.OrdinalIgnoreCase) || resultado.Descricao.Contains("não atendido", StringComparison.OrdinalIgnoreCase) || resultado.Descricao.Contains("ausente", StringComparison.OrdinalIgnoreCase))
                            {
                                var envAtualizado = EnvioService.ObterEnvioCompleto(envio.Id);
                                string contagem = envAtualizado != null && envAtualizado.TentativasEntregaCount > 0 ? $"{envAtualizado.TentativasEntregaCount}/3" : "1/3";
                                NotificacaoCritica?.Invoke(
                                    $"⚠️ Carteiro Não Atendido ({contagem}) - Pedido #{envio.NumeroPedido}",
                                    $"A entrega para {envio.Cliente} ({envio.CodigoRastreio}) não foi atendida no local. Avise o cliente sobre a tentativa {contagem}!"
                                );
                            }
                            else if (st == "Alerta / Retido")
                            {
                                NotificacaoCritica?.Invoke(
                                    $"Alerta nos Correios! (Pedido #{envio.NumeroPedido})",
                                    $"Problema no envio de {envio.Cliente}: {resultado.Descricao}"
                                );
                            }
                        }
                    }
                }
                catch
                {
                    // Ignora falha pontual para prosseguir com outros objetos
                }

                await Task.Delay(400); // Pausa para não sobrecarregar API
            }

            try
            {
                EnvioService.VerificarEArquivarEntregues7Dias();
            }
            catch { }

            LogProgresso?.Invoke("Verificação de rastreios e arquivamento concluídos.");
            AtualizacaoConcluida?.Invoke();
        }
    }
}
