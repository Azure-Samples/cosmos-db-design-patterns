using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos_data_binning
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
