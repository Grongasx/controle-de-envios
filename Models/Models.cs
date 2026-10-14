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

        public string StatusCorreiosBadgeTexto => TemAlertaNaoAtendido ? $"⚠️ Não Atendido ({AlertaTentativaFormatada})" : StatusCorreios;

        public string StatusCorreiosBadgeCor
        {
            get
            {
                if (TemAlertaNaoAtendido) return "#f97316";
                if (Entregue) return "#34d399";
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
        public decimal TotalFrete { get; set; }
        public decimal TotalGastoCorreios { get; set; }
        public decimal DiferencaValorTotal { get; set; }
        public decimal SaldoFreteTotal { get => DiferencaValorTotal; set => DiferencaValorTotal = value; }
        public int TentativasNaoAtendidas { get; set; }
    }
}
