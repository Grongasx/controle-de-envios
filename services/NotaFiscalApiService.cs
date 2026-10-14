using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ControleEnvios.Data;

namespace ControleEnvios.Services
{
    public class NotaFiscalResultado
    {
        public bool Sucesso { get; set; }
        public string NumeroNotaFiscal { get; set; } = string.Empty;
        public string ChaveAcesso { get; set; } = string.Empty;
        public string DataEmissao { get; set; } = string.Empty;
        public string UrlDanfePdf { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;
    }

    /// <summary>
    /// Serviço preparado para integração futura com a API de emissão de Nota Fiscal (NFe / ERP).
    /// </summary>
    public static class NotaFiscalApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static bool EstaConfigurado()
        {
            string url = DatabaseContext.ObterConfig("nfe_api_url", "");
            string token = DatabaseContext.ObterConfig("nfe_api_token", "");
            return !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(token);
        }

        public static async Task<NotaFiscalResultado> ConsultarOuAssociarNotaFiscalAsync(string numeroPedido)
        {
            if (string.IsNullOrWhiteSpace(numeroPedido))
            {
                return new NotaFiscalResultado { Sucesso = false, Mensagem = "Número de pedido não informado." };
            }

            string url = DatabaseContext.ObterConfig("nfe_api_url", "");
            string token = DatabaseContext.ObterConfig("nfe_api_token", "");

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            {
                return new NotaFiscalResultado
                {
                    Sucesso = false,
                    Mensagem = "API de Nota Fiscal ainda não configurada. Configure a URL e Chave Token na aba de Configurações."
                };
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/pedidos/{numeroPedido}/nfe");
                request.Headers.Add("Authorization", $"Bearer {token}");
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string numero = root.TryGetProperty("numero", out var pNum) ? pNum.GetString() ?? "" : "";
                    string chave = root.TryGetProperty("chave", out var pChave) ? pChave.GetString() ?? "" : "";

                    return new NotaFiscalResultado
                    {
                        Sucesso = true,
                        NumeroNotaFiscal = numero,
                        ChaveAcesso = chave,
                        Mensagem = "Nota fiscal localizada com sucesso!"
                    };
                }

                return new NotaFiscalResultado
                {
                    Sucesso = false,
                    Mensagem = $"Resposta da API de NF: {(int)response.StatusCode} {response.ReasonPhrase}"
                };
            }
            catch (Exception ex)
            {
                return new NotaFiscalResultado
                {
                    Sucesso = false,
                    Mensagem = $"Erro ao comunicar com API de NF: {ex.Message}"
                };
            }
        }
    }
}
