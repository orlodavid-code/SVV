using System.Threading.Tasks;

namespace SVV.Services
{
    public abstract class EmailSender
    {
        public abstract Task SendEmailAsync(string toEmail, string subject, string body);
        public abstract Task SendTemplatedEmailAsync(string toEmail, string subject, string templateName, object model);
    }
}