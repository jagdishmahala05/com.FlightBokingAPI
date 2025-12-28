using com.Services.ServiceInterface;
using com.ThirdPartyAPIs.Models;
using com.ThirdPartyAPIs.Models.Flight;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using static com.ThirdPartyAPIs.Constant.AllEnums;

namespace com.Services.Services
{
    public class FlightService : IFlightService
    {
        public IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;

        public FlightService(IConfiguration configuration, IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _env = env;
        }

        public CommonResponse SearchFlight(SearchRequest.RootObject request)
        {
            CommonResponse response = new CommonResponse();
            try
            {
                if (request == null)
                {
                    response.Status = HttpStatusCode.BadRequest;
                    response.Data = "Invalid request payload";
                    return response;
                }

                #region Validation
                if (request.segments == null || request.segments.Length == 0 || request.adults == null || request.adults == 0 || request.cabin == null || request.cabin.Replace(" ", "") == "")
                {
                    response.Data = "Bad Request";
                    response.Status = HttpStatusCode.BadRequest;
                    return response;
                }
                if (Convert.ToInt16(request.adults) > 9)
                {
                    response.Status = HttpStatusCode.BadRequest;
                    response.Data = "Max. 9 Adults Allowed!";
                    return response;
                }
                if (Convert.ToInt16(request.children) > 9)
                {
                    response.Status = HttpStatusCode.BadRequest;
                    response.Data = "Max. 9 Children Allowed!";
                    return response;
                }
                if (Convert.ToInt16(request.infant) > Convert.ToInt16(request.adults))
                {
                    response.Status = HttpStatusCode.BadRequest;
                    response.Data = "Infant should less or equal to adult count!";
                    return response;
                }
                if (Convert.ToInt16(request.JourneyType) != Convert.ToInt16(request.segments.Length))
                {
                    response.Status = HttpStatusCode.BadRequest;
                    response.Data = "Segments is Invalid!";
                    return response;
                }

                foreach (var seg in request.segments)
                {
                    try
                    {
                        if (Convert.ToDateTime(seg.depdate) < DateTime.Now || Convert.ToDateTime(seg.depdate) > DateTime.Now.AddYears(1))
                        {
                            response.Status = HttpStatusCode.BadRequest;
                            response.Data = "Invalid  Date!";
                            return response;
                        }
                    }
                    catch
                    {
                        response.Status = HttpStatusCode.BadRequest;
                        response.Data = "Invalid  Date!";
                        return response;
                    }
                    if (string.IsNullOrEmpty(seg.depcode) || string.IsNullOrEmpty(seg.arrcode))
                    {
                        response.Status = HttpStatusCode.BadRequest;
                        response.Data = "Invalid Request!";
                        return response;
                    }
                }

                if (request.cabin.ToLower().Replace(" ", "") != "c" && request.cabin.ToLower().Replace(" ", "") != "f" && request.cabin.ToLower().Replace(" ", "") != "m" && request.cabin.ToLower().Replace(" ", "") != "w" && request.cabin.ToLower().Replace(" ", "") != "y")
                {
                    response.Status = HttpStatusCode.BadRequest;
                    response.Data = "Invalid Cabin Class!";
                    return response;
                }

                string pathroot = Path.Combine(_env.ContentRootPath, "XmlFiles/");
                string SC = System.Guid.NewGuid().ToString();
                #endregion;

                #region Call API
                var tasks = new List<Task<int>>();
                com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightResponse API_RESULT = new ResultResponse.FlightResponse();
                if (System.IO.File.Exists(pathroot + SC + "_api_call.txt") == false)
                {
                    System.IO.File.WriteAllText(pathroot + SC + "_api_call.txt", "call", System.Text.ASCIIEncoding.UTF8);

                    tasks.Add(Task.Run(async () =>
                    {
                        // Create a client from the factory inside the background task
                        var client = _httpClientFactory.CreateClient(); // optional: CreateClient("Amadeus") if you register a named client
                        var AmadeusService = new com.ThirdPartyAPIs.Amadeus.AmadeusConfig(_configuration, client, _env);
                        API_RESULT = await AmadeusService.Result(request, SC).ConfigureAwait(false);
                        return 0;
                    }));
                }

                Task.WaitAll(tasks.ToArray());
                #endregion;

                response.Data = API_RESULT;
                response.Status = HttpStatusCode.OK;
                return response;
            }
            catch (Exception e)
            {
                response.Status = HttpStatusCode.InternalServerError;
                response.Data = e.Message.ToString();
                return response;
            }
        }

        public CommonResponse Fare_PriceUpsellWithoutPNR(string sc,string id)
        {
            CommonResponse response = new CommonResponse();
            try
            {
                #region Validation
                var xmlfiles = Path.Combine(_env.ContentRootPath, "XmlFiles/");

                if (sc == null || sc == "" || id == null || id == "" || File.Exists(xmlfiles + sc + "_flight_result.json") == false)
                {
                    response.Status = HttpStatusCode.BadRequest;
                    response.Data = "Invalid error!";
                    return response;
                }

                var file_text = "";
                using (var fileStream = new FileStream(xmlfiles + sc + "_flight_result.json", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var streamReader = new StreamReader(fileStream))
                {
                    file_text = streamReader.ReadToEnd();
                }

                //com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightResponse File_data = Newtonsoft.Json.JsonConvert.DeserializeObject<com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightResponse>(file_text);
                ResultResponse.FlightResponse File_data = System.Text.Json.JsonSerializer.Deserialize<ResultResponse.FlightResponse>(file_text);

                ResultResponse.Flightdata selected_flight = File_data.Data.Find(x => x.id == id);
                if (selected_flight == null)
                {
                    response.Status = HttpStatusCode.BadRequest;
                    response.Data = "Invalid selected flight!";
                    return response;
                }

                #endregion;

                #region Call API
                var tasks = new List<Task<int>>();
                ResultResponse.FlightResponse API_RESULT = new ResultResponse.FlightResponse();

                if (selected_flight.supplier == (int)SupplierEnum.Amadeus)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        // Create a client from the factory inside the background task
                        var client = _httpClientFactory.CreateClient(); // optional: CreateClient("Amadeus") if you register a named client
                        var AmadeusService = new com.ThirdPartyAPIs.Amadeus.AmadeusConfig(_configuration, client, _env);
                        API_RESULT = await AmadeusService.Fare_PriceUpsellWithoutPNR(selected_flight,sc,File_data.totaladult,File_data.totalchild,File_data.totalinfant).ConfigureAwait(false);
                        return 0;
                    }));
                }
                Task.WaitAll(tasks.ToArray());
                #endregion;

                response.Data = API_RESULT;
                response.Status = HttpStatusCode.OK;
                return response;
            }
            catch (Exception e)
            {
                response.Status = HttpStatusCode.InternalServerError;
                response.Data = e.Message.ToString();
                return response;
            }
        }
    }
}