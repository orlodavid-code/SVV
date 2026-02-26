using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SVV.Services
{
    public class NotificationWorker : BackgroundService
    {
        private readonly INotificationQueue _queue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NotificationWorker> _logger;

        public NotificationWorker(INotificationQueue queue, IServiceProvider serviceProvider, ILogger<NotificationWorker> logger)
        {
            _queue = queue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Notification Worker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_queue.HasItems())
                    {
                        var item = _queue.Dequeue();

                        // VALIDACIÓN EN WORKER
                        if (item == null || string.IsNullOrWhiteSpace(item.ToEmail))
                        {
                            _logger.LogWarning("Notificación ignorada: destinatario vacío o nulo. Asunto: {Subject}", item?.Subject);
                            continue; // Salta este ítem
                        }

                        _logger.LogInformation($"Processing email to: {item.ToEmail}");

                        using var scope = _serviceProvider.CreateScope();
                        var emailSender = scope.ServiceProvider.GetRequiredService<EmailSender>();

                        if (!string.IsNullOrEmpty(item.TemplateName) && item.Model != null)
                        {
                            await emailSender.SendTemplatedEmailAsync(item.ToEmail, item.Subject, item.TemplateName, item.Model);
                        }
                        else
                        {
                            await emailSender.SendEmailAsync(item.ToEmail, item.Subject, item.Body);
                        }

                        item.SentAt = DateTime.Now;
                        _logger.LogInformation($" Email sent to {item.ToEmail}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing notification queue");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}