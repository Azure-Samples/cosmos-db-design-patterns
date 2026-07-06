namespace Cosmos.DistributedLock
{
    /// <summary>
    /// Derives a monotonically increasing fencing token from the Azure Cosmos DB session
    /// token (its global LSN). Because the LSN only ever increases, it is a natural fencing
    /// token that a downstream system can use to reject stale lock holders.
    /// </summary>
    internal static class SessionTokenParser
    {
        private static readonly char[] segmentSeparator = new[] { '#' };
        private static readonly char[] pkRangeSeparator = new[] { ':' };

        public static long Parse(string sessionToken)
        {
            // Simple session token: {pkrangeid}:{globalLSN}
            // Vector session token: {pkrangeid}:{Version}#{GlobalLSN}#{RegionId1}={LocalLsn1}#...
            var items = sessionToken.Split(pkRangeSeparator, StringSplitOptions.RemoveEmptyEntries);
            var sessionTokenSegments = items[1].Split(segmentSeparator, StringSplitOptions.RemoveEmptyEntries);
            var globalLsnSegmentIndex = sessionTokenSegments.Length == 1 ? 0 : 1;
            return long.Parse(sessionTokenSegments[globalLsnSegmentIndex]);
        }
    }
}
