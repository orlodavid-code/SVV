namespace SVV.Services
{
    public class NotificationItem
    {
        public string ToEmail { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string TemplateName { get; set; }
        public object Model { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? SentAt { get; set; }
    }
}