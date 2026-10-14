using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ControleEnvios.Data;
using ControleEnvios.Models;

namespace ControleEnvios.Services
{
    public class ResultadoRastreio
    {
        public bool Sucesso { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Status { get; set; } = "Pendente";
        public string Descricao { get; set; } = string.Empty;
        public string Detalhe { get; set; } = string.Empty;
        public string UltimoEventoData { get; set; } = string.Empty;
        public string UltimoEventoLocal { get; set; } = string.Empty;
        public bool Entregue { get; set; }
        public string Erro { get; set; } = string.Empty;
        public List<EventoRastreio> Eventos { get; set; } = new();
    }

    public static class CorreiosApiService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.correios.com.br"),
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static string? _tokenCache;
        private static DateTime? _tokenExpiraEm;

        public static async Task<string> ObterTokenAsync(bool forcarRenovacao = false)
        {
            var agora = DateTime.Now;
            if (!forcarRenovacao && !string.IsNullOrEmpty(_tokenCache) && _tokenExpiraEm.HasValue && agora < (_tokenExpiraEm.Value - TimeSpan.FromMinutes(5)))
            {
                return _tokenCache;
            }

            string usuario = DatabaseContext.ObterConfig("correios_usuario").Trim();
            string codigoAcesso = DatabaseContext.ObterConfig("correios_codigo_acesso").Trim();

            if (string.IsNullOrWhiteSpace(codigoAcesso))
            {
                throw new InvalidOperationException("Chave de Acesso da API dos Correios não configurada. Acesse Configurações.");
            }

            // Se for Bearer Token direto
            if (codigoAcesso.StartsWith("eyJ", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(usuario))
            {
                _tokenCache = codigoAcesso;
                _tokenExpiraEm = agora.AddHours(24);
                return _tokenCache;
            }

            string rawAuth = $"{usuario}:{codigoAcesso}";
            string base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawAuth));

            using var request = new HttpRequestMessage(HttpMethod.Post, "/token/v1/autentica");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Falha ao autenticar na API dos Correios (HTTP {(int)response.StatusCode}): {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("token", out var propToken))
            {
                throw new Exception("Token não retornado pela API dos Correios.");
            }

            string token = propToken.GetString() ?? "";
            _tokenCache = token;

            if (root.TryGetProperty("expiraEm", out var propExpira) && DateTime.TryParse(propExpira.GetString(), out var expiraDt))
            {
                _tokenExpiraEm = expiraDt;
            }
            else
            {
                _tokenExpiraEm = agora.AddHours(6);
            }

            return token;
        }

        public static async Task<(bool sucesso, string token, string mensagem)> TestarAutenticacaoAsync(string usuario, string codigoAcesso)
        {
            try
            {
                string chave = (codigoAcesso ?? "").Trim();
                string user = (usuario ?? "").Trim();

                if (string.IsNullOrWhiteSpace(chave))
                {
                    return (false, "", "Informe a Chave de Acesso da API dos Correios.");
                }

                if (chave.StartsWith("eyJ", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(user))
                {
                    return (true, chave, "✓ Chave/Token configurado com sucesso.");
                }

                string rawAuth = $"{user}:{chave}";
                string base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawAuth));

                using var request = new HttpRequestMessage(HttpMethod.Post, "/token/v1/autentica");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, "", $"HTTP {(int)response.StatusCode}: {json}");
                }

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("token", out var pTok))
                {
                    return (true, pTok.GetString() ?? "", "✓ Chave de API validada com sucesso! Conexão ativa com os Correios.");
                }
                return (false, "", "Token não retornado pela API dos Correios.");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        public static async Task<ResultadoRastreio> RastrearObjetoAsync(string codigo)
        {
            string codLimpo = (codigo ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(codLimpo))
            {
                return new ResultadoRastreio { Sucesso = false, Erro = "Código de rastreio vazio." };
            }

            try
            {
                string token = await ObterTokenAsync();

                using var request = new HttpRequestMessage(HttpMethod.Get, $"/srorastro/v1/objetos/{codLimpo}?resultado=T");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);

                // Se token expirou (401), tenta renovar 1 vez
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    token = await ObterTokenAsync(forcarRenovacao: true);
                    using var retryRequest = new HttpRequestMessage(HttpMethod.Get, $"/srorastro/v1/objetos/{codLimpo}?resultado=T");
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    retryRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    response = await _httpClient.SendAsync(retryRequest);
                }

                string content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return new ResultadoRastreio
                    {
                        Sucesso = false,
                        Codigo = codLimpo,
                        Erro = $"Erro HTTP {(int)response.StatusCode}: {content}"
                    };
                }

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (!root.TryGetProperty("objetos", out var objetos) || objetos.GetArrayLength() == 0)
                {
                    return new ResultadoRastreio
                    {
                        Sucesso = true,
                        Codigo = codLimpo,
                        Status = "Não Encontrado",
                        Descricao = "Objeto não consta na base dos Correios",
                        Entregue = false
                    };
                }

                var obj = objetos[0];
                if (!obj.TryGetProperty("eventos", out var eventos) || eventos.GetArrayLength() == 0)
                {
                    string msg = obj.TryGetProperty("mensagem", out var m) ? m.GetString() ?? "" : "Sem eventos";
                    return new ResultadoRastreio
                    {
                        Sucesso = true,
                        Codigo = codLimpo,
                        Status = "Aguardando Postagem",
                        Descricao = msg,
                        Entregue = false
                    };
                }

                var resultado = new ResultadoRastreio
                {
                    Sucesso = true,
                    Codigo = codLimpo
                };

                // O primeiro evento é o mais recente
                var primeiroEvento = eventos[0];
                string codEvento = primeiroEvento.TryGetProperty("codigo", out var c) ? c.GetString() ?? "" : "";
                string descEvento = primeiroEvento.TryGetProperty("descricao", out var d) ? d.GetString() ?? "" : "";
                string dtHr = primeiroEvento.TryGetProperty("dtHrCriado", out var dt) ? dt.GetString() ?? "" : "";
                string detalhe = primeiroEvento.TryGetProperty("detalhe", out var det) ? det.GetString() ?? "" : "";

                string local = "";
                if (primeiroEvento.TryGetProperty("unidade", out var un))
                {
                    if (un.TryGetProperty("endereco", out var end))
                    {
                        string cid = end.TryGetProperty("cidade", out var ci) ? ci.GetString() ?? "" : "";
                        string uf = end.TryGetProperty("uf", out var u) ? u.GetString() ?? "" : "";
                        local = $"{cid}/{uf}".Trim('/');
                    }
                    if (string.IsNullOrEmpty(local) && un.TryGetProperty("tipo", out var tp))
                    {
                        local = tp.GetString() ?? "";
                    }
                }

                resultado.Descricao = descEvento;
                resultado.Detalhe = detalhe;
                resultado.UltimoEventoData = dtHr;
                resultado.UltimoEventoLocal = local;

                // Classificação unificada de status
                string descLower = (descEvento + " " + detalhe).ToLowerInvariant();
                if (descLower.Contains("não entregue") || descLower.Contains("não atendido") || descLower.Contains("destinatário ausente") || descLower.Contains("não pôde ser efetuada") || descLower.Contains("não pode ser efetuada") || descLower.Contains("nova tentativa"))
                {
                    resultado.Status = "Carteiro Não Atendido";
                    resultado.Entregue = false;
                }
                else if (codEvento is "BDE" or "BDI" or "BDR" || descLower.Contains("entregue ao destinatário") || (descLower.Contains("entregue") && !descLower.Contains("não")))
                {
                    resultado.Status = "Entregue";
                    resultado.Entregue = true;
                }
                else if (codEvento == "OEC" || descLower.Contains("saiu para entrega"))
                {
                    resultado.Status = "Saiu para Entrega";
                }
                else if (codEvento == "LDI" || descLower.Contains("aguardando retirada"))
                {
                    resultado.Status = "Aguardando Retirada";
                }
                else if (codEvento is "RO" or "DO" || descLower.Contains("em trânsito") || descLower.Contains("transito") || descLower.Contains("encaminhado"))
                {
                    resultado.Status = "Em Trânsito";
                }
                else if (codEvento == "PO" || descLower.Contains("postado"))
                {
                    resultado.Status = "Postado";
                }
                else if (descLower.Contains("devolvido") || descLower.Contains("extraviado") || descLower.Contains("problema") || descLower.Contains("retido"))
                {
                    resultado.Status = "Alerta / Retido";
                }
                else
                {
                    resultado.Status = descEvento;
                }

                // Processa lista completa de eventos para histórico
                foreach (var ev in eventos.EnumerateArray())
                {
                    string evDt = ev.TryGetProperty("dtHrCriado", out var eDt) ? eDt.GetString() ?? "" : "";
                    string evDesc = ev.TryGetProperty("descricao", out var eDc) ? eDc.GetString() ?? "" : "";
                    string evDet = ev.TryGetProperty("detalhe", out var eDt2) ? eDt2.GetString() ?? "" : "";
                    string evCod = ev.TryGetProperty("codigo", out var eC) ? eC.GetString() ?? "" : "";

                    string evLocal = "";
                    if (ev.TryGetProperty("unidade", out var uObj))
                    {
                        if (uObj.TryGetProperty("endereco", out var uEnd))
                        {
                            string cidVal = uEnd.TryGetProperty("cidade", out var ci) ? ci.GetString() ?? "" : "";
                            string f = uEnd.TryGetProperty("uf", out var uf) ? uf.GetString() ?? "" : "";
                            evLocal = $"{cidVal}/{f}".Trim('/');
                        }
                    }

                    string evDestino = "";
                    if (ev.TryGetProperty("unidadeDestino", out var dObj))
                    {
                        if (dObj.TryGetProperty("endereco", out var dEnd))
                        {
                            string cidDest = dEnd.TryGetProperty("cidade", out var ci2) ? ci2.GetString() ?? "" : "";
                            string f = dEnd.TryGetProperty("uf", out var uf2) ? uf2.GetString() ?? "" : "";
                            evDestino = $"{cidDest}/{f}".Trim('/');
                        }
                    }

                    resultado.Eventos.Add(new EventoRastreio
                    {
                        CodigoRastreio = codLimpo,
                        DataHora = evDt,
                        Descricao = evDesc,
                        Detalhe = evDet,
                        UnidadeLocal = evLocal,
                        UnidadeDestino = evDestino,
                        TipoEvento = evCod
                    });
                }

                return resultado;
            }
            catch (Exception ex)
            {
                return new ResultadoRastreio
                {
                    Sucesso = false,
                    Codigo = codLimpo,
                    Erro = ex.Message
                };
            }
        }
    }
}
