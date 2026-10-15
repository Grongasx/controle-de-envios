using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ControleEnvios.Services
{
    public class EnderecoCepResult
    {
        public bool Sucesso { get; set; }
        public string MensagemErro { get; set; } = string.Empty;
        public string Cep { get; set; } = string.Empty;
        public string Logradouro { get; set; } = string.Empty;
        public string Bairro { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
        public string Ibge { get; set; } = string.Empty;
        public string Ddd { get; set; } = string.Empty;
    }

    public static class CepService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Cache em memória para evitar requisições repetidas ao mesmo CEP
        private static readonly ConcurrentDictionary<string, EnderecoCepResult> _cache = new();

        public static string LimparCep(string cep)
        {
            if (string.IsNullOrWhiteSpace(cep)) return string.Empty;
            return Regex.Replace(cep, @"[^\d]", "").Trim();
        }

        public static string FormatarCep(string cep)
        {
            string limpo = LimparCep(cep);
            if (limpo.Length == 8)
            {
                return $"{limpo.Substring(0, 5)}-{limpo.Substring(5, 3)}";
            }
            return cep;
        }

        /// <summary>
        /// Consulta endereço por CEP de forma assíncrona com fallback:
        /// 1. Tenta ViaCEP (rápido e padronizado)
        /// 2. Se falhar, tenta BrasilAPI
        /// </summary>
        public static async Task<EnderecoCepResult> ConsultarCepAsync(string cepRaw, CancellationToken cancellationToken = default)
        {
            string cep = LimparCep(cepRaw);

            if (string.IsNullOrWhiteSpace(cep) || cep.Length != 8)
            {
                return new EnderecoCepResult
                {
                    Sucesso = false,
                    MensagemErro = "CEP inválido. O CEP deve conter 8 dígitos."
                };
            }

            // Verifica cache
            if (_cache.TryGetValue(cep, out var cachedResult))
            {
                return cachedResult;
            }

            // 1. Tentativa ViaCEP
            try
            {
                string urlViaCep = $"https://viacep.com.br/ws/{cep}/json/";
                using var response = await _httpClient.GetAsync(urlViaCep, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("erro", out var erroProp) || erroProp.GetBoolean() == false)
                    {
                        var res = new EnderecoCepResult
                        {
                            Sucesso = true,
                            Cep = FormatarCep(root.TryGetProperty("cep", out var pCep) ? pCep.GetString() ?? cep : cep),
                            Logradouro = root.TryGetProperty("logradouro", out var pLog) ? pLog.GetString() ?? "" : "",
                            Bairro = root.TryGetProperty("bairro", out var pBai) ? pBai.GetString() ?? "" : "",
                            Cidade = root.TryGetProperty("localidade", out var pCid) ? pCid.GetString() ?? "" : "",
                            Uf = root.TryGetProperty("uf", out var pUf) ? pUf.GetString() ?? "" : "",
                            Ibge = root.TryGetProperty("ibge", out var pIbg) ? pIbg.GetString() ?? "" : "",
                            Ddd = root.TryGetProperty("ddd", out var pDdd) ? pDdd.GetString() ?? "" : ""
                        };

                        _cache[cep] = res;
                        return res;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback para BrasilAPI
            }

            // 2. Fallback BrasilAPI
            try
            {
                string urlBrasilApi = $"https://brasilapi.com.br/api/cep/v1/{cep}";
                using var response = await _httpClient.GetAsync(urlBrasilApi, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var res = new EnderecoCepResult
                    {
                        Sucesso = true,
                        Cep = FormatarCep(root.TryGetProperty("cep", out var pCep) ? pCep.GetString() ?? cep : cep),
                        Logradouro = root.TryGetProperty("street", out var pLog) ? pLog.GetString() ?? "" : "",
                        Bairro = root.TryGetProperty("neighborhood", out var pBai) ? pBai.GetString() ?? "" : "",
                        Cidade = root.TryGetProperty("city", out var pCid) ? pCid.GetString() ?? "" : "",
                        Uf = root.TryGetProperty("state", out var pUf) ? pUf.GetString() ?? "" : ""
                    };

                    _cache[cep] = res;
                    return res;
                }
            }
            catch (Exception ex)
            {
                return new EnderecoCepResult
                {
                    Sucesso = false,
                    MensagemErro = $"Não foi possível consultar o CEP: {ex.Message}"
                };
            }

            return new EnderecoCepResult
            {
                Sucesso = false,
                MensagemErro = "CEP não encontrado nas bases dos Correios / ViaCEP."
            };
        }
    }
}
