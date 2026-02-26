using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace SVV.Services
{
    public class MailKitEmailSender : EmailSender //  Hereda de EmailSender
    {
        private readonly SmtpOptions _smtpOptions;
        private readonly ILogger<MailKitEmailSender> _logger;
        private readonly IRazorViewToStringRenderer _renderer;

        // Constructor: recibe SmtpOptions, ILogger y IRazorViewToStringRenderer (para renderizar templates)
        public MailKitEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<MailKitEmailSender> logger, IRazorViewToStringRenderer renderer)
        {
            _smtpOptions = smtpOptions.Value;
            _logger = logger;
            _renderer = renderer;
        }

        // Implementación del método abstracto para enviar email simple
        public override async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            //  VALIDACIÓN CRÍTICA
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogError(" Intento de enviar correo sin destinatario. Asunto: {Subject}", subject);
                return; // No lanza excepción, solo ignora
            }
            toEmail = toEmail.Trim();

            try
            {
                _logger.LogInformation("Intentando enviar email a {ToEmail} via {Server}:{Port}",
             toEmail, _smtpOptions?.Server, _smtpOptions?.Port);

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_smtpOptions.SenderName, _smtpOptions.SenderEmail));
                message.To.Add(new MailboxAddress("", toEmail)); // Ahora toEmail está validado
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body
                };
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    // Configuración específica para GoDaddy
                    client.Timeout = 60000; // 60 segundos
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    _logger.LogInformation("Conectando a {Server}:{Port} con SSL={UseSSL}",
                        _smtpOptions.Server, _smtpOptions.Port, _smtpOptions.UseSSL);

                    if (_smtpOptions.UseSSL)
                    {
                        await client.ConnectAsync(_smtpOptions.Server, _smtpOptions.Port, true);
                    }
                    else
                    {
                        await client.ConnectAsync(_smtpOptions.Server, _smtpOptions.Port, false);
                    }

                    _logger.LogInformation("Autenticando con usuario: {Username}", _smtpOptions.Username);

                    await client.AuthenticateAsync(_smtpOptions.Username, _smtpOptions.Password);

                    _logger.LogInformation("Enviando mensaje...");
                    await client.SendAsync(message);

                    await client.DisconnectAsync(true);
                    _logger.LogInformation("Email enviado exitosamente a {ToEmail}", toEmail);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email a {ToEmail}", toEmail);
                throw new InvalidOperationException($"Error enviando email a {toEmail}: {ex.Message}", ex);
            }
        }

        // Implementación del método abstracto para enviar email con template
        public override async Task SendTemplatedEmailAsync(string toEmail, string subject, string templateName, object model)
        {
            try
            {
                // Renderizar la vista Razor a string
                var body = await _renderer.RenderViewToStringAsync(templateName, model);

                // Enviar el email usando el método anterior
                await SendEmailAsync(toEmail, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email con template a {ToEmail}", toEmail);
                throw;
            }
        }
    }
}