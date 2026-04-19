namespace MqttPhone.Platforms.Android
{
    public static class SmsStore
    {
        static readonly object _lock = new object();
        static string? _latest;
        public static void SetLatest(string? body)
        {
            lock (_lock)
            {
                _latest = body;
            }
        }

        public static string? GetLatest()
        {
            lock (_lock)
            {
                return _latest;
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _latest = null;
            }
        }
    }
}
