using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using com.Services.ServiceInterface;
using System.Net;
using com.ThirdPartyAPIs.Models;
using com.ThirdPartyAPIs.Models.Flight;
using com.Services.Services;

namespace com.FlightBokingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlightController : ControllerBase
    {
        private readonly IFlightService _flightServices;
        

        public FlightController(IFlightService flightServices)
        {
            _flightServices = flightServices;
        }

        [HttpPost("SearchFlight")]
        public async Task<ActionResult> SearchFlight(SearchRequest.RootObject Search)
        {
            try
            {
                if (Search != null)
                {
                    CommonResponse Response = _flightServices.SearchFlight(Search); 
                    return Ok(Response);
                }
                return Ok(new CommonResponse { Status = HttpStatusCode.BadRequest, Data = "Invalid input" });
            }
            catch (Exception e)
            {
                return Ok(new CommonResponse { Status = HttpStatusCode.InternalServerError, Data = "An Error Found!" });
            }
        }

        
        [HttpPost("Fare_PriceUpsellWithoutPNR")]
        public async Task<ActionResult> Fare_PriceUpsellWithoutPNR(string sc, string id)
        {
            try
            {
                if (!string.IsNullOrEmpty(sc) && !string.IsNullOrEmpty(sc))
                {
                    CommonResponse Response = _flightServices.Fare_PriceUpsellWithoutPNR(sc,id); 
                    return Ok(Response);
                }
                return Ok(new CommonResponse { Status = HttpStatusCode.BadRequest, Data = "Invalid input" });
            }
            catch (Exception e)
            {
                return Ok(new CommonResponse { Status = HttpStatusCode.InternalServerError, Data = "An Error Found!" });
            }
        }

    }
}
