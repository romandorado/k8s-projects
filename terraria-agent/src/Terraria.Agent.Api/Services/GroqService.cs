using System.Net.Http.Json;
using System.Text.Json;
using Terraria.Agent.Api.Models;

namespace Terraria.Agent.Api.Services;

public class GroqService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly ILogger<GroqService> _logger;

    private const string SystemPrompt = @"Eres NARRADOR, el narrador épico del mundo 'MundoSobrinos' en Terraria (Master difficulty).
Eres dramático, gracioso y un poco exagerado. Juegas con sobrinos. Español casual.
Tienes CONOCIMIENTO COMPLETO de Terraria: recetas de crafteo, mecánicas, biomas, NPCs, boss drops, Master difficulty.
Sé conciso pero ÉPICO y CREATIVO. Máximo 200 tokens.";

    public GroqService(HttpClient httpClient, IConfiguration config, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Groq:ApiKey"]!;
        _model = config["Groq:Model"]!;
        _endpoint = config["Groq:Endpoint"]!;
        _logger = logger;
    }

    public async Task<string> GenerateNarrationAsync(string userMessage, string context = "")
    {
        try
        {
            var systemMessage = string.IsNullOrEmpty(context) 
                ? SystemPrompt 
                : $"{SystemPrompt}\n\nContexto actual: {context}";

            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = userMessage }
                },
                max_tokens = 200,
                temperature = 0.8
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Groq API returned {StatusCode}: {Error}", response.StatusCode, errorContent);
                return "El narrador está temporalmente silencioso...";
            }
            var responseString = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            _logger.LogInformation("Groq narration generated: {Content}", content);
            return content ?? "El narrador está pensando...";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to generate narration from Groq");
            return "El narrador está temporalmente silencioso...";
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Groq response");
            return "El narrador está temporalmente silencioso...";
        }
    }

    public async Task<string> GenerateEventNarrationAsync(string prompt, TerrariaStatus? status = null)
    {
        try
        {
            var context = status != null
                ? $"Mundo: MundoSobrinos (Master). Hora: {(status.DayTime ? "día" : "noche")}. " +
                  $"Eventos: {(status.BloodMoon ? "luna de sangre " : "")}{(status.Eclipse ? "eclipse " : "")}. " +
                  $"Jugadores: {string.Join(", ", status.Players.Select(p => p.Name))}."
                : "Mundo: MundoSobrinos (Master). Sin info de estado.";

            var systemMessage = @"Eres el narrador épico del mundo 'MundoSobrinos' en Terraria (Master difficulty).
Sé dramático, gracioso y exagerado. Juegas con sobrinos. Español casual.
Máximo 100 tokens por respuesta. Sé CORTO y ÉPICO.";

            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = $"{systemMessage}\n\nContexto: {context}" },
                    new { role = "user", content = prompt }
                },
                max_tokens = 100,
                temperature = 0.8
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Groq event API returned {StatusCode}", response.StatusCode);
                return "El narrador guarda silencio...";
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            _logger.LogInformation("Event narration: {Content}", content);
            return content ?? "El narrador guarda silencio...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate event narration");
            return "El narrador guarda silencio...";
        }
    }
}
