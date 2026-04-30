using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BillFolder.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillFolder.Infrastructure.Email;

/// <summary>
/// Implementação de IEmailSender usando a API REST do Resend
/// (https://resend.com/docs/api-reference/emails/send-email).
///
/// Optei por HttpClient direto em vez do SDK oficial (Resend NuGet)
/// pra ter controle explícito sobre serialização e evitar lock-in
/// numa versão da SDK que pode mudar a API. A superfície que usamos
/// é pequena (1 endpoint, 4 campos) — não vale a dep extra.
///
/// IsConfigured = true sempre que esta impl é registrada. O DI só
/// registra essa impl quando ResendOptions.ApiKey está preenchido;
/// caso contrário cai no NoOpEmailSender (ver DependencyInjection).
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private const string ResendApiUrl = "https://api.resend.com/emails";

    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => true;

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        // Authorization Bearer header é setado por request — fica explícito
        // e independente da config do HttpClient que possa ser compartilhado.
        using var request = new HttpRequestMessage(HttpMethod.Post, ResendApiUrl)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey) },
            Content = JsonContent.Create(new ResendSendRequest(
                From: _options.FromAddress,
                To: new[] { toEmail },
                Subject: subject,
                Text: body)),
        };

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            // Loga sem expor o body (que pode conter trechos sensíveis).
            // O caller (AuthService) trata exceção como swallow pra não
            // vazar status do email na response — proteção contra
            // user enumeration em forgot-password.
            var statusCode = (int)response.StatusCode;
            _logger.LogWarning(
                "Resend API returned non-success status {StatusCode} when sending to {ToEmail}",
                statusCode,
                toEmail);

            response.EnsureSuccessStatusCode(); // dispara exceção pra caller
        }

        _logger.LogInformation(
            "Sent email via Resend to {ToEmail} (subject: {Subject})",
            toEmail,
            subject);
    }

    /// <summary>
    /// Payload mínimo aceito pela API do Resend. Campos opcionais
    /// (html, cc, bcc, reply_to, tags etc) ficam de fora — quando
    /// virar dor real, expandimos.
    /// </summary>
    private sealed record ResendSendRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] IReadOnlyCollection<string> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text);
}
