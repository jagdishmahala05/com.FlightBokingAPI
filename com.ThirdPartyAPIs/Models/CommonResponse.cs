using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace com.ThirdPartyAPIs.Models
{
    public class CommonResponse
    {
        public HttpStatusCode Status { get; set; }
        public object Data { get; set; }
    }
}
