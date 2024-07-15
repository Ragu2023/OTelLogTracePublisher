using System.Runtime.CompilerServices;

namespace EventGenerator
{
    public static class DateTimeExtensions
    {
        private static readonly DateTime epochDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        // TODO: we need nanosec resolution.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime FromUnixTimestamp(long seconds, long milliseconds = 0)
        {
            DateTime ret = epochDateTime.AddSeconds(seconds).AddMilliseconds(milliseconds);
            return ret;
        }

        // Convert the given DateTime to seconds from Unix epoch.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ConvertToUnixTimestampSeconds(DateTime timestamp)
        {
            DateTime unixTs = timestamp.ToUniversalTime().AddTicks(-epochDateTime.Ticks);

            return unixTs.Ticks / TimeSpan.TicksPerSecond;
        }

        // Convert the given DateTime to seconds from Unix epoch.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UnixTimestampSecondsRoundDown(DateTime timestamp, long interval)
        {
            long seconds = DateTimeExtensions.ConvertToUnixTimestampSeconds(timestamp);

            return seconds - (seconds % interval);
        }

        public static DateTime TruncateToMinute(this DateTime dateTime)
        {
            long alignedTicks = (dateTime.Ticks / TimeSpan.TicksPerMinute) * TimeSpan.TicksPerMinute;
            return new DateTime(alignedTicks, dateTime.Kind);
        }

        /// <summary>
        /// Given a encoded timestamp in msgpack, convert to DateTime.
        /// </summary>
        /// <param name="tsObj">The ext for timestamp</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime FromMsgpackTimestamp(object tsObj)
        {
            switch (tsObj)
            {
                case uint secs:
                    return DateTimeExtensions.FromUnixTimestamp(secs);
                case int secs:
                    return DateTimeExtensions.FromUnixTimestamp(secs);
                case DateTime dt:
                    return dt;
                default:
                    // Trace.TraceWarning($"{nameof(ProcessMsgpackPackedForwardAsync)}: Invalid timestamp in the event. Use current time instead");
                    return DateTime.UtcNow;
            }
        }
    }
}
