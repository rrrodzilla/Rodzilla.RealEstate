using System;

namespace Rodzilla.RealEstate.Models
{
    public class SearchRequestor
    {
        public string TimeSpan => DateTime.Now.ToUniversalTime().Subtract(new TimeSpan(1, 15, 30, 0))
            .ToString("yyyy-MM-ddTHH:mm:ss.fff");

        public int ResultsLimit => 3;
    }
}
