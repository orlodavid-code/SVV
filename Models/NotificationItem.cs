using System;

namespace SVV.Models
{
    public class NotificationItem
    {
        public string ToEmail { get; set; }
        public string Subject { get; set; }
        public string BodyHtml { get; set; }
        public string BodyText { get; set; }
        public string Reference { get; set; } // id solicitud u otro
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    }
}