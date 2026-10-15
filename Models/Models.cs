using System;
using System.Collections.Generic;
using System.Windows;

namespace ControleEnvios.Models
{
    public class Fornecedor
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Apelido { get; set; } = string.Empty;
        public string CnpjCpf { get; set; } = string.Empty;
        public string Cep { get; set; } = string.Empty;
        public string Logradouro { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Complemento { get; set; } = string.Empty;
        public string Bairro { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string DisplayName => !string.IsNullOrWhiteSpace(Apelido) ? $"{Apelido} ({Nome})" : Nome;
        public string EnderecoCompleto => $"{Logradouro}, {Numero} {Complemento} - {Bairro}, {Cidade}/{Uf} - CEP: {Cep}".Replace("  ", " ").Trim(' ', '-');
    }

    public class ItemEnvio
    {
        public int Id { get; set; }
        public int EnvioId { get; set; }
        public string NomeProduto { get; set; } = string.Empty;
        public string Referencia { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Tamanho { get; set; } = string.Empty;
        public int Quantidade { get; set; } = 1;
        public decimal ValorUnitario { get; set; }
        public decimal ValorTotal { get; set; }
    }

    public class EventoRastreio
    {
        public int Id { get; set; }
        public int EnvioId { get; set; }
        public string CodigoRastreio { get; set; } = string.Empty;
        public string DataHora { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Detalhe { get; set; } = string.Empty;
        public string UnidadeLocal { get; set; } = string.Empty;
        public string UnidadeDestino { get; set; } = string.Empty;
        public string TipoEvento { get; set; } = string.Empty;
    }

    public class Envio
    {
        public int Id { get; set; }
        public string NumeroPedido { get; set; } = string.Empty;
        public string Cliente { get; set; } = string.Empty;
        public string ClienteCpf { get; set; } = string.Empty;
        public string ClienteCelular { get; set; } = string.Empty;
        public string ClienteEmail { get; set; } = string.Empty;

        public string EnderecoEntrega { get; set; } = string.Empty;
        public string ComplementoEntrega { get; set; } = string.Empty;
        public string CidadeDestino { get; set; } = string.Empty;
        public string UfDestino { get; set; } = string.Empty;
        public string CepDestino { get; set; } = string.Empty;

        public string FormaEnvioLoja { get; set; } = string.Empty;
        public double PesoGramas { get; set; }
        public decimal ValorPedido { get; set; }
        public decimal ValorFreteLoja { get; set; }

        public string OrigemTipo { get; set; } = "PROPRIO"; // PROPRIO ou DROPSHIPPING
        public int? FornecedorId { get; set; }
        public string FornecedorNome { get; set; } = string.Empty;
        public string FornecedorApelido { get; set; } = string.Empty;

        public string NotaFiscal { get; set; } = string.Empty;
        public string CodigoRastreio { get; set; } = string.Empty;
        public string TipoRastreio { get; set; } = "NORMAL"; // NORMAL ou PRE_POSTAGEM
        public string DataEnvio { get; set; } = string.Empty;
        public decimal ValorCorreios { get; set; }

        public string StatusCorreios { get; set; } = "Pendente";
        public string DescricaoStatus { get; set; } = string.Empty;
        public string UltimoEventoData { get; set; } = string.Empty;
        public string UltimoEventoLocal { get; set; } = string.Empty;
        public bool Entregue { get; set; }

        public string Observacoes { get; set; } = string.Empty;
        public string CaminhoPdf { get; set; } = string.Empty;
        public bool DesativarAvisos { get; set; }
        public bool Arquivado { get; set; }
        public string DataEntrega { get; set; } = string.Empty;
        public string DataArquivamento { get; set; } = string.Empty;
        public string ProtocoloReclamacao { get; set; } = string.Empty;
        public string DataReclamacao { get; set; } = string.Empty;
        public string StatusReclamacao { get; set; } = string.Empty;
        public bool TemReclamacaoAberta => !string.IsNullOrWhiteSpace(ProtocoloReclamacao);

        // Campos dedicados para Logística Reversa / Devolução
        public string CodigoAutorizacaoReversa { get; set; } = string.Empty;
        public string DataLimiteReversa { get; set; } = string.Empty;
        public string StatusReversa { get; set; } = string.Empty;

        public bool IsReversa => (TipoRastreio ?? "").Equals("REVERSA", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(CodigoAutorizacaoReversa);
        public bool IsAguardandoPostagemReversa => IsReversa && string.IsNullOrWhiteSpace(CodigoRastreio);
        public bool IsPostadoReversa => IsReversa && !string.IsNullOrWhiteSpace(CodigoRastreio);

        public bool ReversaPrestesAVencer
        {
            get
            {
                if (!IsReversa || !string.IsNullOrWhiteSpace(CodigoRastreio) || string.IsNullOrWhiteSpace(DataLimiteReversa))
                    return false;

                if (DateTime.TryParse(DataLimiteReversa, out var dtLimite) ||
                    DateTime.TryParseExact(DataLimiteReversa, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dtLimite))
                {
                    var diasRestantes = (dtLimite.Date - DateTime.Today).TotalDays;
                    return diasRestantes >= 0 && diasRestantes <= 2;
                }
                return false;
            }
        }

        public bool ReversaVencida
        {
            get
            {
                if (!IsReversa || !string.IsNullOrWhiteSpace(CodigoRastreio) || string.IsNullOrWhiteSpace(DataLimiteReversa))
                    return false;

                if (DateTime.TryParse(DataLimiteReversa, out var dtLimite) ||
                    DateTime.TryParseExact(DataLimiteReversa, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dtLimite))
                {
                    return dtLimite.Date < DateTime.Today;
                }
                return false;
            }
        }

        public Visibility BadgeReversaVisibility => IsReversa ? Visibility.Visible : Visibility.Collapsed;
        public string BadgeReversaTexto => IsReversa ? "🔄 REVERSA" : "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public List<ItemEnvio> Itens { get; set; } = new();
        public List<EventoRastreio> Historico { get; set; } = new();

        public string ResumoProdutos { get; set; } = string.Empty;

        public bool IsFreteGratisOuRetirada
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FormaEnvioLoja)) return false;
                string f = FormaEnvioLoja.Trim().ToLowerInvariant();
                return f.Contains("grátis") || f.Contains("gratis") || f.Contains("retirada") || f.Contains("retirar");
            }
        }

        public decimal DiferencaFrete
        {
            get
            {
                if (IsFreteGratisOuRetirada) return 0;
                return ValorCorreios > 0 ? (ValorFreteLoja - ValorCorreios) : 0;
            }
        }

        public decimal DiferencaValor => DiferencaFrete;

        public string DiferencaFreteFormatada
        {
            get
            {
                if (IsFreteGratisOuRetirada) return "—";
                if (ValorCorreios <= 0) return "—";
                decimal dif = ValorFreteLoja - ValorCorreios;
                if (dif > 0) return $"+ R$ {dif:F2}";
                if (dif < 0) return $"- R$ {Math.Abs(dif):F2}";
                return "R$ 0,00";
            }
        }

        public string DiferencaValorFormatada => DiferencaFreteFormatada;

        public string DiferencaFreteCor
        {
            get
            {
                if (IsFreteGratisOuRetirada || ValorCorreios <= 0) return "#94a3b8";
                decimal dif = ValorFreteLoja - ValorCorreios;
                if (dif > 0) return "#34d399";
                if (dif < 0) return "#f87171";
                return "#38bdf8";
            }
        }

        public string DiferencaValorCor => DiferencaFreteCor;

        public string OrigemFormatada
        {
            get
            {
                if (OrigemTipo == "PROPRIO") return "Estoque Próprio";
                if (!string.IsNullOrWhiteSpace(FornecedorApelido)) return $"Drop: {FornecedorApelido}";
                if (!string.IsNullOrWhiteSpace(FornecedorNome)) return $"Drop: {FornecedorNome}";
                return "Dropshipping";
            }
        }

        public bool TemPdfVinculado => !string.IsNullOrWhiteSpace(CaminhoPdf);

        public bool CobradoSemPdf => !DesativarAvisos && ValorCorreios > 0 && !TemPdfVinculado;

        public Visibility CobradoSemPdfVisibility => CobradoSemPdf ? Visibility.Visible : Visibility.Collapsed;

        public string AvisoCobradoSemPdf => CobradoSemPdf
            ? $"⚠️ Cobrado nos Correios ({ValorCorreios:C2}) — PDF da Tray ainda não extraído!"
            : string.Empty;

        // Contador de tentativas de entrega não atendidas (1/3, 2/3, etc)
        public int TentativasEntregaCount { get; set; }

        public bool TemAlertaNaoAtendido => !DesativarAvisos && !Entregue && TentativasEntregaCount > 0;

        public Visibility AlertaNaoAtendidoVisibility => TemAlertaNaoAtendido ? Visibility.Visible : Visibility.Collapsed;

        public string AlertaTentativaFormatada => TentativasEntregaCount > 0 ? $"{TentativasEntregaCount}/3" : "";

        // Formatação inteligente de CPF ou CNPJ
        public string ClienteDocumentoFormatado
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ClienteCpf)) return "—";
                string limpo = System.Text.RegularExpressions.Regex.Replace(ClienteCpf, @"[^\d]", "").Trim();
                if (limpo.Length == 11)
                    return $"{limpo.Substring(0, 3)}.{limpo.Substring(3, 3)}.{limpo.Substring(6, 3)}-{limpo.Substring(9, 2)}";
                if (limpo.Length == 14)
                    return $"{limpo.Substring(0, 2)}.{limpo.Substring(2, 3)}.{limpo.Substring(5, 3)}/{limpo.Substring(8, 4)}-{limpo.Substring(12, 2)}";
                return ClienteCpf;
            }
        }

        // ==========================================
        // CÁLCULO DE DIAS ÚTEIS E DEMORA EXCESSIVA
        // ==========================================

        public static int CalcularDiasUteis(DateTime inicio, DateTime fim)
        {
            if (inicio > fim) return 0;
            int dias = 0;
            DateTime atual = inicio.Date.AddDays(1);
            while (atual <= fim.Date)
            {
                if (atual.DayOfWeek != DayOfWeek.Saturday && atual.DayOfWeek != DayOfWeek.Sunday)
                {
                    dias++;
                }
                atual = atual.AddDays(1);
            }
            return dias;
        }

        private static DateTime? ParseDataFlexivel(string? dataStr)
        {
            if (string.IsNullOrWhiteSpace(dataStr)) return null;
            string[] formatos = {
                "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy"
            };
            if (DateTime.TryParseExact(dataStr.Trim(), formatos, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParse(dataStr.Trim(), out var dt2))
                return dt2;
            return null;
        }

        public int DiasSemMovimentacao
        {
            get
            {
                if (Entregue) return 0;
                var dtUltimo = ParseDataFlexivel(UltimoEventoData) ?? ParseDataFlexivel(DataEnvio) ?? CreatedAt;
                return CalcularDiasUteis(dtUltimo, DateTime.Now);
            }
        }

        public int DiasEmTransito
        {
            get
            {
                var dtInicio = ParseDataFlexivel(DataEnvio) ?? CreatedAt;
                var dtFim = Entregue ? (ParseDataFlexivel(DataEntrega) ?? DateTime.Now) : DateTime.Now;
                return CalcularDiasUteis(dtInicio, dtFim);
            }
        }

        public bool TemDemoraExcessiva
        {
            get
            {
                if (DesativarAvisos || Entregue || Arquivado) return false;

                // 1. Pré-postagem que não foi postada fisicamente após 3 dias úteis
                if (TipoRastreio == "PRE_POSTAGEM" && (StatusCorreios == "Pendente" || StatusCorreios == "Pré-postagem"))
                {
                    return DiasEmTransito >= 3;
                }

                // Se não tem código de rastreio ou nunca foi postado, não mede demora dos Correios
                if (string.IsNullOrWhiteSpace(CodigoRastreio)) return false;

                // 2. Parado sem nenhuma movimentação dos Correios há mais de 5 dias úteis (risco de extravio/retenção)
                if (DiasSemMovimentacao >= 5) return true;

                // 3. Trânsito excessivo por modalidade
                string modalidade = (FormaEnvioLoja ?? "").ToUpperInvariant();
                if (modalidade.Contains("SEDEX") && DiasEmTransito >= 5) return true;
                if (modalidade.Contains("PAC") && DiasEmTransito >= 10) return true;
                if (!modalidade.Contains("SEDEX") && !modalidade.Contains("PAC") && DiasEmTransito >= 8) return true;

                return false;
            }
        }

        public string MotivoDemora
        {
            get
            {
                if (!TemDemoraExcessiva) return string.Empty;

                if (TipoRastreio == "PRE_POSTAGEM" && (StatusCorreios == "Pendente" || StatusCorreios == "Pré-postagem"))
                    return $"Pré-postagem aguardando envio há {DiasEmTransito} dias úteis";

                if (DiasSemMovimentacao >= 5)
                    return $"Parado nos Correios há {DiasSemMovimentacao} dias úteis sem movimentação";

                string modalidade = (FormaEnvioLoja ?? "").ToUpperInvariant();
                if (modalidade.Contains("SEDEX"))
                    return $"SEDEX em trânsito há {DiasEmTransito} dias úteis (limite padrão: 5d)";
                if (modalidade.Contains("PAC"))
                    return $"PAC em trânsito há {DiasEmTransito} dias úteis (limite padrão: 10d)";

                return $"Encomenda em trânsito há {DiasEmTransito} dias úteis";
            }
        }

        public Visibility DemoraBadgeVisibility => TemDemoraExcessiva ? Visibility.Visible : Visibility.Collapsed;

        public string DemoraBadgeTexto
        {
            get
            {
                if (!TemDemoraExcessiva) return "";
                if (DiasSemMovimentacao >= 5) return $"⏱️ Parado ({DiasSemMovimentacao}d úteis)";
                return $"⏱️ Demora ({DiasEmTransito}d úteis)";
            }
        }

        public string StatusCorreiosBadgeTexto
        {
            get
            {
                if (IsReversa)
                {
                    if (IsAguardandoPostagemReversa)
                    {
                        if (ReversaVencida) return "⚠️ Reversa Expirada (Não Postado)";
                        if (ReversaPrestesAVencer) return "⚠️ Reversa Vencendo (Aguardando)";
                        return "🔄 Reversa: Aguardando Postagem";
                    }
                    if (Entregue) return "🔄 Reversa: Devolvido / Entregue";
                    return $"🔄 Reversa: {StatusCorreios}";
                }

                if (TemAlertaNaoAtendido) return $"⚠️ Não Atendido ({AlertaTentativaFormatada})";
                if (TemDemoraExcessiva) return $"{StatusCorreios} • {DemoraBadgeTexto}";
                return StatusCorreios;
            }
        }

        public string StatusCorreiosBadgeCor
        {
            get
            {
                if (IsReversa)
                {
                    if (ReversaVencida) return "#ef4444";
                    if (ReversaPrestesAVencer) return "#f59e0b";
                    if (IsAguardandoPostagemReversa) return "#818cf8";
                    if (Entregue) return "#34d399";
                    return "#a855f7";
                }

                if (TemAlertaNaoAtendido) return "#f97316";
                if (Entregue) return "#34d399";
                if (TemDemoraExcessiva) return "#f43f5e"; // Vermelho suave de alerta de atraso
                if (StatusCorreios == "Saiu para Entrega") return "#c084fc";
                if (StatusCorreios == "Aguardando Retirada") return "#fbbf24";
                if (StatusCorreios == "Alerta / Retido") return "#ef4444";
                return "#38bdf8";
            }
        }

        public string AvisoTentativaCliente =>
            $"Olá {Cliente}! 📦 Informamos que os Correios realizaram a {TentativasEntregaCount}ª tentativa de entrega do seu pedido #{NumeroPedido}" +
            (!string.IsNullOrWhiteSpace(CodigoRastreio) ? $" (Rastreio: {CodigoRastreio})" : "") +
            ", porém o carteiro não foi atendido no local. Haverá uma nova tentativa nos próximos dias úteis. Por favor, certifique-se de que haverá alguém no endereço para receber!";

        public string AvisoAtrasoCliente =>
            $"Olá {Cliente}! 📦 Acompanhamos o envio do seu pedido #{NumeroPedido}" +
            (!string.IsNullOrWhiteSpace(CodigoRastreio) ? $" (Código: {CodigoRastreio})" : "") +
            $" e identificamos que ele está demorando mais do que o habitual pelos Correios ({MotivoDemora}).\n\n" +
            "Já estamos acompanhando a ocorrência junto aos Correios para que a entrega seja concluída o quanto antes. Qualquer novidade entraremos em contato imediatamente!";

        public string InstrucoesReversaCliente =>
            $"Olá {Cliente}! 🔄 Seguem as instruções e sua Autorização de Postagem Oficial dos Correios para a devolução do pedido #{NumeroPedido}:\n\n" +
            $"🔢 Código de Autorização: {(string.IsNullOrWhiteSpace(CodigoAutorizacaoReversa) ? CodigoRastreio : CodigoAutorizacaoReversa)}\n" +
            (!string.IsNullOrWhiteSpace(DataLimiteReversa) ? $"📅 Validade para Postagem: até {DataLimiteReversa}\n\n" : "\n") +
            "📦 Como postar na agência:\n" +
            "1. Embale o produto devidamente protegido (em caixa ou envelope pardo);\n" +
            "2. Anexe a Nota Fiscal ou Declaração de Conteúdo do lado de fora da embalagem;\n" +
            "3. Dirija-se a qualquer agência dos Correios e informe o Código de Autorização acima ao atendente.\n\n" +
            "O envio já está 100% pago pela nossa loja! Assim que você postar, nosso sistema identificará o rastreamento automaticamente. Qualquer dúvida estamos à disposição! 😊";

        public string DadosReclamacaoCorreios =>
            $"DADOS PARA MANIFESTAÇÃO / RECLAMAÇÃO (PI) NOS CORREIOS:\n" +
            $"• Código de Rastreio: {CodigoRastreio}\n" +
            $"• Pedido da Loja: #{NumeroPedido}\n" +
            $"• Destinatário: {Cliente}\n" +
            $"• Documento: {ClienteDocumentoFormatado}\n" +
            $"• Destino: {EnderecoEntrega}, {CidadeDestino}/{UfDestino} - CEP {CepDestino}\n" +
            $"• Data de Postagem: {(string.IsNullOrWhiteSpace(DataEnvio) ? CreatedAt.ToString("dd/MM/yyyy") : DataEnvio)}\n" +
            $"• Último Evento: {UltimoEventoData} - {UltimoEventoLocal} ({DescricaoStatus})\n" +
            $"• Diagnóstico: {MotivoDemora}\n" +
            $"• Modalidade: {FormaEnvioLoja}";
    }

    public class Metricas
    {
        public int Total { get; set; }
        public int Entregues { get; set; }
        public int EmTransito { get; set; }
        public int SaiuEntrega { get; set; }
        public int AguardandoRetirada { get; set; }
        public int Alertas { get; set; }
        public int PrePostagem { get; set; }
        public int Arquivados { get; set; }
        public int Reversas { get; set; }
        public decimal TotalFrete { get; set; }
        public decimal TotalGastoCorreios { get; set; }
        public decimal DiferencaValorTotal { get; set; }
        public decimal SaldoFreteTotal { get => DiferencaValorTotal; set => DiferencaValorTotal = value; }
        public int TentativasNaoAtendidas { get; set; }
        public int DemoraExcessiva { get; set; }
    }
}
