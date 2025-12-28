using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.ThirdPartyAPIs.Models;
using com.ThirdPartyAPIs.Models.Flight;

namespace com.Services.ServiceInterface
{
    public interface IFlightService
    {
        CommonResponse SearchFlight(SearchRequest.RootObject Search);
        CommonResponse Fare_PriceUpsellWithoutPNR(string sc, string id);
    }
}
