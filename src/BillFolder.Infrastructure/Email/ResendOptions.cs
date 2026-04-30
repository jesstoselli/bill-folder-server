namespace BillFolder.Infrastructure.Email;

/// <summary>
/// Bind do appsettings (section "Resend") com a config do provedor de
/// email. ApiKey vazio = email desabilitado (DI cai no NoOpEmailSender,
/// AuthService expõe DevCode na response).
///
/// Em produção a key vem via env var Resend__ApiKey no docker-compose
/// (NÃO commitar valor real em appsettings.json).
/// </summary>
public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>
    /// API key gerada em https://resend.com/api-keys. Começa com "re_".
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Remetente padrão dos emails. Formato RFC 5322:
    /// "Nome &lt;email@dominio.tld&gt;" — o domínio precisa estar
    /// verificado no Resend (DKIM + SPF). Usar `noreply@billfolder.app`
    /// pra deixar claro pro user que respostas não são lidas.
    /// </summary>
    public string FromAddress { get; set; } = "BillFolder <noreply@billfolder.app>";
}
