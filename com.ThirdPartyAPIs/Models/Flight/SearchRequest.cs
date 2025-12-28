using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.ThirdPartyAPIs.Models.Flight
{
    public class SearchRequest
    {
        public class RootObject
        {

            public int adults { get; set; }
            public int children { get; set; }
            public int infant { get; set; }
            public string cabin { get; set; }
            public int JourneyType { get; set; }
            public string ip { get; set; }
            public bool stops { get; set; }
            public segments[] segments { get; set; }
        }

        public class segments
        {
            public string depcode { get; set; }
            public string arrcode { get; set; }
            public string depdate { get; set; }
        }
    }
}
