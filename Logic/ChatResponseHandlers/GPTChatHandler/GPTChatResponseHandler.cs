using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Logic;

/// <summary>
/// A chat response handler that forwards user input to the OpenAI Chat Completions API
/// and returns the assistant's reply.
///
/// Configuration:
/// - Set environment variable OPENAI_API_KEY with your API key.
/// - Optionally set OPENAI_MODEL (default: gpt-4o-mini).
/// - Optionally set OPENAI_BASE_URL to override API base (default: https://api.openai.com/v1).
/// - Optionally set OPENAI_SYSTEM_PROMPT to customize the assistant instruction.
/// </summary>
public class GPTChatResponseHandler : IChatResponseHandler
{
    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<string> BuildResponseAsync(string message, string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // No key available; fail gracefully with a minimal response
            return "No OpenAI API Key available.";
        }

        var baseUrl = "https://api.openai.com/v1";
        var model = "gpt-5-nano";

        var url = baseUrl + "/responses";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new ResponsesRequest
        {
            model = model,
            input = message,
            temperature = 0.6
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return "received"; // graceful fallback
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var result = await JsonSerializer.DeserializeAsync<ResponsesResponse>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);

            string? content = null;
            if (result?.output != null)
            {
                var sb = new StringBuilder();
                foreach (var item in result.output)
                {
                    if (item?.type == "message" && item.content != null)
                    {
                        foreach (var part in item.content)
                        {
                            if (part?.type == "output_text" && !string.IsNullOrEmpty(part.text))
                            {
                                sb.Append(part.text);
                            }
                        }
                    }
                }
                content = sb.ToString();
            }

            if (string.IsNullOrWhiteSpace(content))
                return "Agentic reply is empty!";

            return content.Trim();
        }
        catch
        {
            // Network or parsing error; do not break chat
            return "Unable to retrieve agentic reply ;(";
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Minimal DTOs for OpenAI Chat Completions API
    private sealed class ChatCompletionRequest
    {
        public string model { get; set; } = string.Empty;
        public ChatMessage[] messages { get; set; } = Array.Empty<ChatMessage>();
        public double? temperature { get; set; }
    }

    private sealed class ChatMessage
    {
        public string role { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        public Choice[] choices { get; set; } = Array.Empty<Choice>();
    }

    private sealed class Choice
    {
        public ChatMessage? message { get; set; }
    }

    // DTOs for OpenAI Responses API
    private sealed class ResponsesRequest
    {
        public string model { get; set; } = string.Empty;
        public object? input { get; set; }
        public double? temperature { get; set; }
        
        public string prompt { get; set; } = "You are a helpful and concise ai agent";
    }

    private sealed class ResponsesResponse
    {
        public ResponsesOutputItem[]? output { get; set; }
    }

    private sealed class ResponsesOutputItem
    {
        public string? type { get; set; }
        public string? id { get; set; }
        public string? role { get; set; }
        public ResponsesContentItem[]? content { get; set; }
    }

    private sealed class ResponsesContentItem
    {
        public string? type { get; set; }
        public string? text { get; set; }
    }
}
