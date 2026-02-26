using System.Collections.Generic;

namespace SVV.Services
{
    public interface INotificationQueue
    {
        void Enqueue(NotificationItem item);
        NotificationItem Dequeue();
        bool HasItems();
        IEnumerable<NotificationItem> GetAllItems();
    }
}