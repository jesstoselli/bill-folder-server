namespace BillFolder.Application.Abstractions.Email;

/// <summary>
/// Abstração de envio de email transacional (reset de senha, futuramente
/// confirmação de signup, alertas de ciclo etc).
///
/// Implementações:
///  - ResendEmailSender: chama API do Resend (provedor oficial em prod).
///  - NoOpEmailSender: fallback usado quando o ApiKey do Resend não está
///    configurado. Em dev permite testar o fluxo sem provider real;
///    AuthService detecta isso e expõe o código em DevCode na response.
///
/// IsConfigured indica se a impl atual envia email de verdade. AuthService
/// usa pra decidir se inclui DevCode na response (que NÃO deve ir em prod).
/// </summary>
public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken ct = default);
}

/// <summary>
/// Impl de fallback: log + no-op. Usada em dev quando o ApiKey do Resend
/// está vazio. Permite testar o fluxo de reset sem depender de provider
/// externo — o código fica no response (DevCode) pra você copiar.
/// </summary>
public sealed class NoOpEmailSender : IEmailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        // No-op intencional. Logger é injetado pelo Program.cs se necessário,
        // mas aqui evitamos dep extra: a info de "email não enviado, está em
        // DevCode" é responsabilidade do AuthService informar ao caller.
        return Task.CompletedTask;
    }
}
