namespace DataBinning
{
    internal static class Utility
    {
        public static DateTime GetNextPublishTime(DateTime time)
        {
            time = time.AddMinutes(1);
            return new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, DateTimeKind.Utc);
        }
    }
}
