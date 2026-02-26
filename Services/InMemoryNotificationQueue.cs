using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SVV.Services
{
    public class InMemoryNotificationQueue : INotificationQueue
    {
        private readonly ConcurrentQueue<NotificationItem> _queue = new();

        public void Enqueue(NotificationItem item)
        {
            _queue.Enqueue(item);
        }

        public NotificationItem Dequeue()
        {
            _queue.TryDequeue(out var item);
            return item;
        }

        public bool HasItems()
        {
            return !_queue.IsEmpty;
        }

        public IEnumerable<NotificationItem> GetAllItems()
        {
            return _queue.ToArray();
        }
    }
}