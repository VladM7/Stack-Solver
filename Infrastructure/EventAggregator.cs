namespace Stack_Solver.Infrastructure
{
    public interface IEventAggregator
    {
        void Publish<TMessage>(TMessage message);
        void Subscribe<TMessage>(Action<TMessage> handler);
        void Unsubscribe<TMessage>(Action<TMessage> handler);
    }

    public class EventAggregator : IEventAggregator
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = [];

        public void Publish<TMessage>(TMessage message)
        {
            var t = typeof(TMessage);
            if (_handlers.TryGetValue(t, out var list))
            {
                foreach (var h in list.ToArray())
                {
                    try { ((Action<TMessage>)h).Invoke(message); }
                    catch { }
                }
            }
        }

        public void Subscribe<TMessage>(Action<TMessage> handler)
        {
            var t = typeof(TMessage);
            if (!_handlers.TryGetValue(t, out var list))
            {
                list = [];
                _handlers[t] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public void Unsubscribe<TMessage>(Action<TMessage> handler)
        {
            var t = typeof(TMessage);
            if (_handlers.TryGetValue(t, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0) _handlers.Remove(t);
            }
        }
    }
}
