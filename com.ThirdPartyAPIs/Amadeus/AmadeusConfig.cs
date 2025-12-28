using com.ThirdPartyAPIs.Models.Flight;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using static com.ThirdPartyAPIs.Constant.AllEnums;


namespace com.ThirdPartyAPIs.Amadeus
{
    public class AmadeusConfig
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IWebHostEnvironment _env;

        public AmadeusConfig(IConfiguration configuration, HttpClient httpClient, IWebHostEnvironment env)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _env = env ?? throw new ArgumentNullException(nameof(env));
        }
        // Public result entry — now performs session create then sends pricing request with session header

        #region Reslut comment code

        public async Task<com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightResponse> Result_old(com.ThirdPartyAPIs.Models.Flight.SearchRequest.RootObject searchcriteria)
        {
            if (searchcriteria == null) throw new ArgumentNullException(nameof(searchcriteria));
            if (searchcriteria.segments == null) throw new ArgumentException("searchcriteria.segments is null", nameof(searchcriteria));

            var flightdatamain = new Models.Flight.ResultResponse.FlightResponse();

            // Read password from configuration (avoid hard-coding). Fallback to previous literal if missing.
            string password = _configuration["FlightSettings:AirPassword"] ?? "U9MbJZjzR^EP";
            string url = _configuration["FlightSettings:AirProductionURL"] ?? throw new InvalidOperationException("FlightSettings:AirProductionURL missing");

            // Build security header values
            Guid Messageguid = Guid.NewGuid();
            string guidString = Messageguid.ToString();

            byte[] nonceBytes = new byte[16];
            RandomNumberGenerator.Fill(nonceBytes);
            string encodedNonce = Convert.ToBase64String(nonceBytes);

            string created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss+00:00");

            // Password Digest (WS-Security style). SHA1 used to match remote requirement.
            string passSHA;
            //using (var sha1 = SHA1.Create())
            //{
            //    var passwordHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(password));
            //    var createdBytes = Encoding.ASCII.GetBytes(created);

            //    var combined = new byte[nonceBytes.Length + createdBytes.Length + passwordHash.Length];
            //    Buffer.BlockCopy(nonceBytes, 0, combined, 0, nonceBytes.Length);
            //    Buffer.BlockCopy(createdBytes, 0, combined, nonceBytes.Length, createdBytes.Length);
            //    Buffer.BlockCopy(passwordHash, 0, combined, nonceBytes.Length + createdBytes.Length, passwordHash.Length);

            //    var digest = sha1.ComputeHash(combined);
            //    passSHA = Convert.ToBase64String(digest);
            //}
            byte[] nonce = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(nonce);
            }
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] passwordHash = sha1.ComputeHash(passwordBytes);

                byte[] timestampBytes = Encoding.ASCII.GetBytes(created);
                byte[] combinedBytes = new byte[nonce.Length + timestampBytes.Length + passwordHash.Length];

                Buffer.BlockCopy(nonce, 0, combinedBytes, 0, nonce.Length);
                Buffer.BlockCopy(timestampBytes, 0, combinedBytes, nonce.Length, timestampBytes.Length);
                Buffer.BlockCopy(passwordHash, 0, combinedBytes, nonce.Length + timestampBytes.Length, passwordHash.Length);

                byte[] passSHABytes = sha1.ComputeHash(combinedBytes);
                passSHA = Convert.ToBase64String(passSHABytes);
            }

            // Build XML body (use StringBuilder-like behaviour via string interpolation for readability)
            var sb = new StringBuilder();
            sb.Append("<Fare_MasterPricerTravelBoardSearch xmlns=\"http://xml.amadeus.com/FMPTBQ_23_4_1A\">");

            sb.Append("<numberOfUnit>");
            sb.Append("<unitNumberDetail><numberOfUnits>" + _configuration["FlightSettings:NoOfCombinations"] + "</numberOfUnits><typeOfUnit>RC</typeOfUnit></unitNumberDetail>");
            sb.Append("<unitNumberDetail><numberOfUnits>" + (searchcriteria.adults + searchcriteria.children) + "</numberOfUnits><typeOfUnit>PX</typeOfUnit></unitNumberDetail>");
            sb.Append("</numberOfUnit>");

            // Adult pax
            sb.Append("<paxReference><ptc>IIT</ptc><ptc>ADT</ptc>");
            for (int i = 0; i < searchcriteria.adults; i++)
                sb.Append("<traveller><ref>" + (i + 1) + "</ref></traveller>");
            sb.Append("</paxReference>");

            // Children
            if (searchcriteria.children > 0)
            {
                sb.Append("<paxReference><ptc>CNN</ptc><ptc>INN</ptc>");
                for (int i = searchcriteria.adults; i < searchcriteria.adults + searchcriteria.children; i++)
                    sb.Append("<traveller><ref>" + (i + 1) + "</ref></traveller>");
                sb.Append("</paxReference>");
            }

            // Infants - ensure closing traveller tag and closing paxReference
            if (searchcriteria.infant > 0)
            {
                sb.Append("<paxReference><ptc>ITF</ptc><ptc>IN</ptc>");
                for (int i = 0; i < searchcriteria.infant; i++)
                    sb.Append("<traveller><ref>" + (i + 1) + "</ref><infantIndicator>1</infantIndicator></traveller>");
                sb.Append("</paxReference>");
            }

            // Fare options (kept same as your original content)
            sb.Append("<fareOptions>");
            sb.Append("<pricingTickInfo><pricingTicketing>");
            sb.Append("<priceType>TAC</priceType><priceType>RP</priceType><priceType>ET</priceType><priceType>RU</priceType><priceType>CUC</priceType><priceType>MNR</priceType><priceType>RW</priceType>");
            sb.Append("</pricingTicketing></pricingTickInfo>");
            sb.Append("<corporate><corporateId><corporateQualifier>RW</corporateQualifier><identity>387833</identity></corporateId></corporate>");
            sb.Append("<feeIdDescription><feeId><feeType>FFI</feeType><feeIdNumber>1</feeIdNumber></feeId></feeIdDescription>");
            sb.Append("<conversionRate><conversionRateDetail><currency>KES</currency></conversionRateDetail></conversionRate>");
            sb.Append("</fareOptions>");

            sb.Append("<travelFlightInfo><cabinId><cabinQualifier>RC</cabinQualifier>");
            sb.Append("<cabin>" + (searchcriteria.cabin ?? string.Empty).ToUpper() + "</cabin>");
            sb.Append("</cabinId></travelFlightInfo>");

            // Segments — ensure segRef increments
            int index = 1;
            foreach (var seg in searchcriteria.segments)
            {
                sb.Append("<itinerary>");
                sb.Append("<requestedSegmentRef><segRef>" + index + "</segRef></requestedSegmentRef>");
                sb.Append("<departureLocalization><departurePoint><locationId>" + (seg.depcode ?? string.Empty).ToUpper() + "</locationId></departurePoint></departureLocalization>");
                sb.Append("<arrivalLocalization><arrivalPointDetails><locationId>" + (seg.arrcode ?? string.Empty).ToUpper() + "</locationId></arrivalPointDetails></arrivalLocalization>");
                sb.Append("<timeDetails><firstDateTimeDetail><date>" + Convert.ToDateTime(seg.depdate).ToString("ddMMyy") + "</date></firstDateTimeDetail></timeDetails>");
                sb.Append("</itinerary>");
                index++;
            }

            sb.Append("</Fare_MasterPricerTravelBoardSearch>");

            // Final SOAP envelope
            string finalXML =
$@"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""
 xmlns:awsse=""http://xml.amadeus.com/2010/06/Session_v3""
 xmlns:sec=""http://xml.amadeus.com/2010/06/Security_v1""
 xmlns:typ=""http://xml.amadeus.com/2010/06/Types_v1""
 xmlns:app=""http://xml.amadeus.com/2010/06/AppMdw_CommonTypes_v3"">
<s:Header>
    <a:MessageID xmlns:a=""http://www.w3.org/2005/08/addressing"">{guidString}</a:MessageID>
    <a:Action xmlns:a=""http://www.w3.org/2005/08/addressing"">{_configuration["FlightSettings:AirSoapAction"]}FMPTBQ_23_4_1A</a:Action>
    <a:To xmlns:a=""http://www.w3.org/2005/08/addressing"">{url}</a:To>
<Security xmlns=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
<oas:UsernameToken xmlns:oas=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"" xmlns:oas1=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"" oas1:Id=""UsernameToken-1""><oas:Username>{_configuration["FlightSettings:AirUserName"]}</oas:Username><oas:Nonce EncodingType=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"">{encodedNonce}</oas:Nonce> <oas:Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest"">{passSHA}</oas:Password><oas1:Created>{created}</oas1:Created></oas:UsernameToken>
</Security>

<h:AMA_SecurityHostedUser xmlns:h=""http://xml.amadeus.com/2010/06/Security_v1"">
        <h:UserID POS_Type=""1"" 
                  PseudoCityCode=""{_configuration["FlightSettings:AirOfficeID"]}"" 
                  AgentDutyCode=""{_configuration["FlightSettings:AirDutyCode"]}"" 
                  RequestorType=""U"" />
    </h:AMA_SecurityHostedUser>
</s:Header>
<s:Body>
{sb}
</s:Body>
</s:Envelope>";

            // Call API using modern HttpClient (async). Avoid blocking and deprecated APIs.
            string actionHeader = _configuration["FlightSettings:AirSoapAction"] + "FMPTBQ_23_4_1A";
            string result;
            try
            {
                result = await CallApiAsync(finalXML, actionHeader).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Surface exceptions instead of swallowing — caller can decide how to handle.
                throw new InvalidOperationException("Amadeus request failed", ex);
            }

            // Save logs if configured
            try
            {
                var xmlfiles = _configuration["StaticFiles:xmlfiles"];
                if (!string.IsNullOrWhiteSpace(xmlfiles))
                {
                    Directory.CreateDirectory(xmlfiles);
                    File.WriteAllText(Path.Combine(xmlfiles, "Request.xml"), finalXML, Encoding.UTF8);
                    File.WriteAllText(Path.Combine(xmlfiles, "Response.xml"), result ?? string.Empty, Encoding.UTF8);
                }
            }
            catch
            {
                // Swallow logging I/O errors — do not break main flow
            }

            // TODO: parse result to populate flightdatamain (kept out so behavior unchanged)
            return flightdatamain;
        }

        // Replaced synchronous WebRequest usage with async HttpClient usage.
        public async Task<string> CallApiAsync(string requestBody, string actionUrl)
        {
            if (string.IsNullOrWhiteSpace(requestBody)) throw new ArgumentException("requestBody is empty", nameof(requestBody));
            var baseUrl = _configuration["FlightSettings:AirProductionURL"] ?? throw new InvalidOperationException("FlightSettings:AirProductionURL missing");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            // SOAPAction header may be required by the server; add it.
            httpRequest.Headers.Remove("SOAPAction");
            httpRequest.Headers.Add("SOAPAction", actionUrl);
            httpRequest.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

            var response = await _httpClient.SendAsync(httpRequest).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Return body (could be SOAP fault); caller handles success vs fault
            return body;
        }

        // kept for compatibility; thin wrapper that uses CallApiAsync
        public Task<string> CallSoapAsync(string url, string action, string xmlBody)
        {
            // Note: original CallSoapAsync used _httpClient and identical behavior.
            return CallApiAsync(xmlBody, action);
        }

        //public async Task<com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightResponse> Result(com.ThirdPartyAPIs.Models.Flight.SearchRequest.RootObject data)
        //{


        #endregion Reslut comment code

        public async Task<com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightResponse> Result(com.ThirdPartyAPIs.Models.Flight.SearchRequest.RootObject data, string SC)
        {
            //ConcurrentBag<FlightPriceData> concurrentPricing = [];
            var flightdatamain = new Models.Flight.ResultResponse.FlightResponse();

            try
            {
                string password = "U9MbJZjzR^EP";
                string currency = "INR";
                //string searchkey = GetSearchKey(data);
                //CBTFlightBooking.Models.Flight.cachedresponse.Rootobject casheresponse = new Models.Flight.cachedresponse.Rootobject();

                //casheresponse = GetCachedResponse(searchkey);

                //if (!string.IsNullOrEmpty(casheresponse?.response))
                //{
                //    lstPricing = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FlightPriceData>>(casheresponse.response);
                //    return lstPricing;
                //}

                #region Request
                var url = _configuration["FlightSettings:AirProductionURL"];
                Guid Messageguid = Guid.NewGuid();
                string guidString = Messageguid.ToString();
                byte[] nonce = new byte[16];
                using (var rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(nonce);
                }
                DateTime timestamp = DateTime.UtcNow;
                string formattedTimestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss+00:00");
                string encodedNonce = Convert.ToBase64String(nonce);
                string passSHA = "";
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                    byte[] passwordHash = sha1.ComputeHash(passwordBytes);

                    byte[] timestampBytes = Encoding.ASCII.GetBytes(formattedTimestamp);
                    byte[] combinedBytes = new byte[nonce.Length + timestampBytes.Length + passwordHash.Length];

                    Buffer.BlockCopy(nonce, 0, combinedBytes, 0, nonce.Length);
                    Buffer.BlockCopy(timestampBytes, 0, combinedBytes, nonce.Length, timestampBytes.Length);
                    Buffer.BlockCopy(passwordHash, 0, combinedBytes, nonce.Length + timestampBytes.Length, passwordHash.Length);

                    byte[] passSHABytes = sha1.ComputeHash(combinedBytes);
                    passSHA = Convert.ToBase64String(passSHABytes);
                }


                StringBuilder sb = new();
                sb.Append("<Fare_MasterPricerTravelBoardSearch xmlns=\"http://xml.amadeus.com/FMPTBQ_23_4_1A\" >");
                sb.Append("<numberOfUnit>");
                sb.Append("<unitNumberDetail>");
                sb.Append("<numberOfUnits>" + _configuration["FlightSettings:NoOfCombinations"] + "</numberOfUnits>");
                sb.Append("<typeOfUnit>RC</typeOfUnit>");
                sb.Append("</unitNumberDetail>");
                sb.Append("<unitNumberDetail>");
                sb.Append("<numberOfUnits>" + (data.adults + data.children) + "</numberOfUnits>");
                sb.Append("<typeOfUnit>PX</typeOfUnit>");
                sb.Append("</unitNumberDetail>");
                sb.Append("</numberOfUnit>");
                sb.Append("<paxReference>");
                sb.Append("<ptc>IIT</ptc>");
                sb.Append("<ptc>ADT</ptc>");
                for (int a = 0; a < data.adults; a++)
                {
                    sb.Append("<traveller>");
                    sb.Append("<ref>" + (a + 1) + "</ref>");
                    sb.Append("</traveller>");
                }
                sb.Append("</paxReference>");
                if (data.children > 0)
                {
                    sb.Append("<paxReference>");
                    sb.Append("<ptc>CNN</ptc>");
                    sb.Append("<ptc>INN</ptc>");
                }
                for (int c = data.adults; c < (data.adults + data.children); c++)
                {
                    sb.Append("<traveller>");
                    sb.Append("<ref>" + (c + 1) + "</ref>");
                    sb.Append("</traveller>");
                }
                if (data.children > 0)
                    sb.Append("</paxReference>");
                if (data.infant > 0)
                {
                    sb.Append("<paxReference>");
                    sb.Append("<ptc>ITF</ptc>");
                    sb.Append("<ptc>IN</ptc>");
                }
                for (int c = 0; c < data.infant; c++)
                {
                    sb.Append("<traveller>");
                    sb.Append("<ref>" + (c + 1) + "</ref>");
                    sb.Append("<infantIndicator>1</infantIndicator>");
                    sb.Append("</traveller>");
                }
                if (data.infant > 0)
                    sb.Append("</paxReference>");
                sb.Append("<fareOptions>");
                sb.Append("<pricingTickInfo>");
                sb.Append("<pricingTicketing>");
                sb.Append("<priceType>TAC</priceType>");
                sb.Append("<priceType>RP</priceType>");
                sb.Append("<priceType>ET</priceType>");
                sb.Append("<priceType>RU</priceType>");
                sb.Append("<priceType>CUC</priceType>");
                sb.Append("<priceType>MNR</priceType>");
                sb.Append("<priceType>RW</priceType>");
                //if (data.isRF == "1")
                //sb.Append("<priceType>RF</priceType>");
                //if (data.isRF == "2")
                //    sb.Append("<priceType>NRE</priceType>");
                sb.Append("</pricingTicketing>");
                sb.Append("</pricingTickInfo>");
                sb.Append("<corporate>");
                sb.Append("<corporateId>");
                sb.Append("<corporateQualifier>RW</corporateQualifier>");
                sb.Append("<identity>387833</identity>");
                //sb.Append("<identity>SEATONLY</identity>");
                sb.Append("</corporateId>");
                sb.Append("</corporate>");
                sb.Append("<feeIdDescription>");
                sb.Append("<feeId>");
                sb.Append("<feeType>FFI</feeType>");
                sb.Append("<feeIdNumber>1</feeIdNumber>");
                sb.Append("</feeId>");
                sb.Append("</feeIdDescription>");
                sb.Append("<conversionRate>");
                sb.Append("<conversionRateDetail>");
                sb.Append("<currency>KES</currency>");
                sb.Append("</conversionRateDetail>");
                sb.Append("</conversionRate>");
                sb.Append("</fareOptions>");
                sb.Append("<travelFlightInfo>");
                sb.Append("<cabinId>");
                sb.Append("<cabinQualifier>RC</cabinQualifier>");
                sb.Append("<cabin>" + data.cabin + "</cabin>");
                sb.Append("</cabinId>");
                sb.Append("</travelFlightInfo>");
                if (data.JourneyType != 3)
                {
                    for (int i = 1; i <= data.JourneyType; i++)
                    {
                        sb.Append("<itinerary>");
                        sb.Append("<requestedSegmentRef>");
                        sb.Append("<segRef>" + i + "</segRef>");
                        sb.Append("</requestedSegmentRef>");
                        sb.Append("<departureLocalization>");
                        sb.Append("<departurePoint>");
                        sb.Append("<locationId>" + data.segments[i - 1].depcode + "</locationId>");
                        sb.Append("</departurePoint>");
                        sb.Append("</departureLocalization>");
                        sb.Append("<arrivalLocalization>");
                        sb.Append("<arrivalPointDetails>");
                        sb.Append("<locationId>" + data.segments[i - 1].arrcode + "</locationId>");
                        sb.Append("</arrivalPointDetails>");
                        sb.Append("</arrivalLocalization>");
                        sb.Append("<timeDetails>");
                        sb.Append("<firstDateTimeDetail>");
                        sb.Append("<date>" + Convert.ToDateTime(data.segments[i - 1].depdate).ToString("ddMMyy") + "</date>");
                        sb.Append("</firstDateTimeDetail>");
                        sb.Append("</timeDetails>");
                        sb.Append("</itinerary>");
                    }
                }

                sb.Append("</Fare_MasterPricerTravelBoardSearch>");

                var content = new StringContent("<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\""
                + " xmlns:awsse=\"http://xml.amadeus.com/2010/06/Session_v3\""
                + " xmlns:sec=\"http://xml.amadeus.com/2010/06/Security_v1\""
                + " xmlns:typ=\"http://xml.amadeus.com/2010/06/Types_v1\""
                + " xmlns:app=\"http://xml.amadeus.com/2010/06/AppMdw_CommonTypes_v3\">"
                + "<s:Header>"
                + "<a:MessageID xmlns:a=\"http://www.w3.org/2005/08/addressing\">" + guidString + "</a:MessageID>"
                + "<a:Action xmlns:a=\"http://www.w3.org/2005/08/addressing\">" + _configuration["FlightSettings:AirSoapAction"] + "FMPTBQ_23_4_1A</a:Action>"
                + "<a:To xmlns:a=\"http://www.w3.org/2005/08/addressing\">" + _configuration["FlightSettings:AirProductionURL"] + "</a:To>"
                + "<Security xmlns=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\">"
                + "<oas:UsernameToken xmlns:oas=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\""
                + " xmlns:oas1=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd\" oas1:Id=\"UsernameToken-1\">"
                + "<oas:Username>" + _configuration["FlightSettings:AirUserName"] + "</oas:Username>"
                + "<oas:Nonce EncodingType=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary\">" + encodedNonce + "</oas:Nonce>"
                + "<oas:Password Type=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest\">" + passSHA + "</oas:Password>"
                + "<oas1:Created>" + formattedTimestamp + "</oas1:Created>"
                + "</oas:UsernameToken>"
                + "</Security>"
                + "<h:AMA_SecurityHostedUser xmlns:h=\"http://xml.amadeus.com/2010/06/Security_v1\">"
                + "<h:UserID POS_Type=\"1\" PseudoCityCode=\"" + _configuration["FlightSettings:AirOfficeID"]
                + "\" AgentDutyCode=\"" + _configuration["FlightSettings:AirDutyCode"] + "\" RequestorType=\"U\"/>"
                + "</h:AMA_SecurityHostedUser>"
                + "</s:Header>"
                + "<s:Body>"
                + sb.ToString()
                + "</s:Body>"
                + "</s:Envelope>", null, "application/xml");
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var reqt = new HttpRequestMessage(HttpMethod.Post, url);
                reqt.Headers.Add("soapAction", _configuration["FlightSettings:AirSoapAction"] + "FMPTBQ_23_4_1A");
                reqt.Content = content;
                var requestContent = content.ReadAsStringAsync().Result;

                #endregion Request

                #region Response

                //SaveAmadeusLog(requestContent, "-", "Request Fare_MasterPricerTravelBoardSearch");
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                var response = await _httpClient.SendAsync(reqt);
                var re = response.Content.ReadAsStringAsync().Result;
                //SaveAmadeusLog( , re, "Response Fare_MasterPricerTravelBoardSearch");
                //System.IO.File.WriteAllText("C:\\xmlfiles\\Fare_MasterPricerTravelBoardSearch Request.xml", requestContent);
                //System.IO.File.WriteAllText("C:\\xmlfiles\\Fare_MasterPricerTravelBoardSearch Response.xml", re);

                var xmlfiles = Path.Combine(_env.ContentRootPath, "XmlFiles/");
                if (!string.IsNullOrWhiteSpace(xmlfiles))
                {
                    if (!Directory.Exists(xmlfiles))
                        Directory.CreateDirectory(xmlfiles);
                    File.WriteAllText(Path.Combine(xmlfiles, SC + "_Flight_amadeus_request.xml"), requestContent, Encoding.UTF8);
                    File.WriteAllText(Path.Combine(xmlfiles, SC + "_Flight_amadeus_response.xml"), re ?? string.Empty, Encoding.UTF8);
                }

                XmlSerializer serializer = new XmlSerializer(typeof(com.ThirdPartyAPIs.Amadeus.Flight.Fare_MasterPricerTravelBoardSearch_response.Envelope));
                StringReader rdr = new StringReader(re);
                com.ThirdPartyAPIs.Amadeus.Flight.Fare_MasterPricerTravelBoardSearch_response.Envelope result = (com.ThirdPartyAPIs.Amadeus.Flight.Fare_MasterPricerTravelBoardSearch_response.Envelope)serializer.Deserialize(rdr);
                rdr.Close();

                //File.WriteAllText(xml_files + "Fare_MasterPricerTravelBoardSearch_response_" + sc + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(result), System.Text.ASCIIEncoding.UTF8);



                if (result == null || result.Body == null || result.Body.Fare_MasterPricerTravelBoardSearchReply == null || result.Body.Fare_MasterPricerTravelBoardSearchReply.errorMessage != null || result.Body.Fare_MasterPricerTravelBoardSearchReply.recommendation == null || result.Body.Fare_MasterPricerTravelBoardSearchReply.recommendation.Length == 0)
                {
                    return flightdatamain;
                }

                #region result bind


                #region Itinerary Bound

                //string importantstaticdata = System.Configuration.ConfigurationManager.AppSettings["importantfiles"].ToString();

                //string airlinefilelistHtmls = System.IO.File.ReadAllText(importantstaticdata + "Airlinelist.json", System.Text.ASCIIEncoding.UTF8);
                //List<Models.Flight.airport_search.airlinefile> airlinefilelist = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Models.Flight.airport_search.airlinefile>>(airlinefilelistHtmls);

                //string listairportjson = System.IO.File.ReadAllText(importantstaticdata + "trawexairportlist.json", System.Text.ASCIIEncoding.UTF8);
                //List<Models.Flight.airport_search.airportlist> airportfilelist = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Models.Flight.airport_search.airportlist>>(listairportjson);

                //List<pepmytrip_api.Models.Flight.Result.response.AirPortInfo> Airport_Filter = new List<Models.Flight.Result.response.AirPortInfo>();
                //List<pepmytrip_api.Models.Flight.Result.response.FlightFilter> Airline_Filter = new List<Models.Flight.Result.response.FlightFilter>();
                //List<pepmytrip_api.Models.Flight.Result.response.FlightFilter_price> FlightFilter_price = new List<Models.Flight.Result.response.FlightFilter_price>();


                //List<string> Outbound_Destination_Filter = new List<string>();
                //List<string> Inbound_Destination_Filter = new List<string>();
                //pepmytrip_api.Models.Flight.Result.response.stopsdata Outboundstops = new pepmytrip_api.Models.Flight.Result.response.stopsdata();
                //pepmytrip_api.Models.Flight.Result.response.stopsdata Inboundstops = new pepmytrip_api.Models.Flight.Result.response.stopsdata();
                List<com.ThirdPartyAPIs.Models.Flight.ResultResponse.Flightdata> Flightdata = new List<Models.Flight.ResultResponse.Flightdata>();
                List<com.ThirdPartyAPIs.Models.Flight.ResultResponse.Flightdata> Flightdata_secret = new List<Models.Flight.ResultResponse.Flightdata>();

                List<string> flight_uniqueids = new List<string>();

                #region manage pcc code

                //if (ds_flight_configuration.Tables.Count > 2)
                //{
                //    DataTable dt_pcc = ds_flight_configuration.Tables[2];

                //    if (dt_pcc.Rows.Count > 0)
                //    {
                //        int searchindex = 1;
                //        foreach (DataRow dr in dt_pcc.Rows)
                //        {
                //            pepmytrip_api.Models.Flight.Result.response.Flight pccresult = new Models.Flight.Result.response.Flight();

                //            pccresult = otherpcccall(ds_flight_configuration, currency, currency_exchangerate, dr["pcc_code"].ToString(), searchindex, admin_fixed_allow, admin_percentage_allow, admin_fixed, admin_percentage, Dt_airline_discount_markup, dt_airlinemarkup, sc, searchcriteria);


                //            if (pccresult.success == true)
                //            {
                //                if (Airline_Filter.Count == 0)
                //                {
                //                    Airline_Filter = pccresult.data.Filter.Airline;
                //                }
                //                else
                //                {
                //                    Airline_Filter.AddRange(pccresult.data.Filter.Airline);
                //                }
                //                if (Airport_Filter.Count == 0)
                //                {
                //                    Airport_Filter = pccresult.data.Filter.AirportFilter;
                //                }
                //                else
                //                {
                //                    Airport_Filter.AddRange(pccresult.data.Filter.AirportFilter);
                //                }
                //                if (Flightdata.Count == 0)
                //                {
                //                    Flightdata = pccresult.data.Data;
                //                }
                //                else
                //                {
                //                    Flightdata.AddRange(pccresult.data.Data);
                //                }

                //                if (flight_uniqueids.Count == 0)
                //                {
                //                    flight_uniqueids = pccresult.data.flight_unique_ids;
                //                }
                //                else
                //                {
                //                    flight_uniqueids.AddRange(pccresult.data.flight_unique_ids);
                //                }
                //            }
                //            searchindex++;
                //        }
                //    }
                //}

                #endregion

                int result_index = 1;

                foreach (Amadeus_wsdl.Fare_MasterPricerTravelBoardSearchReplyRecommendation recommendation in result.Body.Fare_MasterPricerTravelBoardSearchReply.recommendation)
                {

                    #region Tarriff info
                    double apitotalprice = 0;
                    double totalprice = 0;
                    double baseprice = 0;
                    double taxprice = 0;
                    List<Models.Flight.ResultResponse.tarriffinfo> tarriffinfo = new List<Models.Flight.ResultResponse.tarriffinfo>();
                    List<Models.Flight.ResultResponse.pax_fareDetailsBySegment> pax_fareDetailsBySegment = new List<Models.Flight.ResultResponse.pax_fareDetailsBySegment>();

                    List<Models.Flight.ResultResponse.fareDetailsgroup> fareDetailsgroup = new List<Models.Flight.ResultResponse.fareDetailsgroup>();

                    foreach (Amadeus_wsdl.Fare_MasterPricerTravelBoardSearchReplyRecommendationPaxFareProduct paxFareProduct in recommendation.paxFareProduct)
                    {
                        int quantity = paxFareProduct.paxReference[0].traveller.Length;

                        double api_total_price_per_leg = Convert.ToDouble(paxFareProduct.paxFareDetail.totalFareAmount) * quantity;
                        double api_tax_price_per_leg = Convert.ToDouble(paxFareProduct.paxFareDetail.totalTaxAmount) * quantity;
                        double api_base_price_per_leg = api_total_price_per_leg - api_tax_price_per_leg;

                        double net_total_price_per_leg = api_total_price_per_leg;
                        double net_tax_price_per_leg = api_tax_price_per_leg;
                        double net_base_price_per_leg = api_base_price_per_leg;



                        double base_price_per_leg = Convert.ToDouble((net_base_price_per_leg).ToString("#0.00"));
                        double total_price_per_leg = Convert.ToDouble((net_total_price_per_leg).ToString("#0.00"));
                        double tax_price_per_leg = total_price_per_leg - base_price_per_leg;


                        string PaxType = paxFareProduct.paxReference[0].ptc[0];
                        string PaxType_text = paxFareProduct.paxReference[0].ptc[0];
                        if (PaxType.ToLower() == "adt")
                        {
                            PaxType_text = "Adult";
                        }
                        else if (PaxType.ToLower() == "cnn")
                        {
                            PaxType_text = "Child";
                        }
                        else if (PaxType.ToLower() == "inf")
                        {
                            PaxType_text = "Infant";
                        }

                        totalprice = totalprice + net_total_price_per_leg;
                        baseprice = baseprice + net_base_price_per_leg;
                        taxprice = taxprice + net_tax_price_per_leg;


                        apitotalprice = apitotalprice + net_total_price_per_leg;

                        tarriffinfo.Add(new Models.Flight.ResultResponse.tarriffinfo()
                        {
                            api_baseprice = api_base_price_per_leg,
                            api_tax = api_tax_price_per_leg,
                            api_totalprice = api_total_price_per_leg,
                            net_baseprice = net_base_price_per_leg,
                            net_tax = net_tax_price_per_leg,
                            net_totalprice = net_total_price_per_leg,
                            baseprice = Convert.ToDouble(base_price_per_leg.ToString("#0.00")),
                            currency = currency,
                            paxtype = PaxType,
                            paxtype_text = PaxType_text,
                            paxid = "",// travelerPricings.travelerId,
                            per_pax_total_price = Convert.ToDouble(total_price_per_leg.ToString("#0.00")),
                            quantity = quantity,
                            tax = Convert.ToDouble(tax_price_per_leg.ToString("#0.00")),
                            totalprice = Convert.ToDouble((total_price_per_leg).ToString("#0.00"))
                        });



                        int segindex = 0;
                        foreach (Amadeus_wsdl.Fare_MasterPricerTravelBoardSearchReplyRecommendationPaxFareProductFareDetails fareDetails in paxFareProduct.fareDetails)
                        {
                            int leg_index = 1;
                            foreach (Amadeus_wsdl.Fare_MasterPricerTravelBoardSearchReplyRecommendationPaxFareProductFareDetailsGroupOfFares groupOfFares in fareDetails.groupOfFares)
                            {
                                var pax_fareDetailsBySegment_find_obj = pax_fareDetailsBySegment.Find(x => x.segmentid == leg_index.ToString());
                                if (pax_fareDetailsBySegment_find_obj == null)
                                {
                                    pax_fareDetailsBySegment.Add(new Models.Flight.ResultResponse.pax_fareDetailsBySegment()
                                    {
                                        segmentid = leg_index.ToString(),
                                        segment_pax_detail = new List<Models.Flight.ResultResponse.segment_pax_detail>() { new Models.Flight.ResultResponse.segment_pax_detail() {
                                    baggage = null,
                                    cabin = groupOfFares.productInformation.cabinProduct[0].cabin,
                                    class_code = groupOfFares.productInformation.cabinProduct[0].rbd,
                                    fareBasis =groupOfFares.productInformation.fareProductDetail.fareBasis,
                                    paxid = "",//travelerPricings.travelerId,
                                    paxtype = PaxType,
                                }},
                                    });
                                }
                                else
                                {
                                    pax_fareDetailsBySegment_find_obj.segment_pax_detail.Add(new Models.Flight.ResultResponse.segment_pax_detail()
                                    {
                                        baggage = null,
                                        cabin = groupOfFares.productInformation.cabinProduct[0].cabin,
                                        class_code = groupOfFares.productInformation.cabinProduct[0].rbd,
                                        fareBasis = groupOfFares.productInformation.fareProductDetail.fareBasis,
                                        paxid = "",//travelerPricings.travelerId,
                                        paxtype = PaxType,
                                    });
                                }

                                var pax_fareDetailsgroup_obj = fareDetailsgroup.Find(x => x.segmentid == segindex);
                                if (pax_fareDetailsgroup_obj == null)
                                {
                                    fareDetailsgroup.Add(new Models.Flight.ResultResponse.fareDetailsgroup()
                                    {
                                        segmentid = segindex,
                                        groupfare = new List<Models.Flight.ResultResponse.groupfare>() { new Models.Flight.ResultResponse.groupfare() {

                                    cabin = groupOfFares.productInformation.cabinProduct[0].cabin,
                                    rbd = groupOfFares.productInformation.cabinProduct[0].rbd,
                                    fareBasis =groupOfFares.productInformation.fareProductDetail.fareBasis,
                                    groupindex=leg_index,
                                }},
                                    });
                                }
                                else
                                {
                                    pax_fareDetailsgroup_obj.groupfare.Add(new Models.Flight.ResultResponse.groupfare()
                                    {
                                        cabin = groupOfFares.productInformation.cabinProduct[0].cabin,
                                        rbd = groupOfFares.productInformation.cabinProduct[0].rbd,
                                        fareBasis = groupOfFares.productInformation.fareProductDetail.fareBasis,
                                        groupindex = leg_index,
                                    });
                                }
                                leg_index++;
                            }
                            segindex++;
                        }
                    }

                    #endregion;

                    foreach (Amadeus_wsdl.ReferenceInfoType22 Itinerary in recommendation.segmentFlightRef)
                    //foreach (Amadeus_wsdl.ReferenceInformationType11 Itinerary in recommendation.segmentFlightRef)
                    {
                        Boolean calculate_markup = true;
                        int totaltime = 0;
                        Amadeus_wsdl.ReferencingDetailsType_191583C2[] Out_in_bound_list = Array.FindAll(Itinerary.referencingDetail, item => item.refQualifier == "S");

                        if (Out_in_bound_list == null || Out_in_bound_list.Length == 0)
                        {
                            continue;
                        }

                        #region Slice and Dice
                        Amadeus_wsdl.ReferencingDetailsType_191583C2[] slice_dice_list = Array.FindAll(Itinerary.referencingDetail, item => item.refQualifier == "A");
                        List<Models.Flight.ResultResponse.spirit_airline> Slice_dice_value = new List<Models.Flight.ResultResponse.spirit_airline>();
                        if (slice_dice_list != null && slice_dice_list.Length > 0)
                        {
                            foreach (var slice_dice_list_each in slice_dice_list)
                            {
                                var specificRecDetails = Array.Find(recommendation.specificRecDetails, item => item.specificRecItem.referenceType == slice_dice_list_each.refQualifier && item.specificRecItem.refNumber == slice_dice_list_each.refNumber);
                                if (specificRecDetails != null)
                                {
                                    foreach (var specificProductDetails in specificRecDetails.specificProductDetails)
                                    {
                                        foreach (var fareContextDetails in specificProductDetails.fareContextDetails)
                                        {
                                            int leg = 1;
                                            if (fareContextDetails.cnxContextDetails.Length > 0)
                                            {
                                                foreach (var cnxContextDetails in fareContextDetails.cnxContextDetails)
                                                {
                                                    if (cnxContextDetails.fareCnxInfo.contextDetails.Length > 0)
                                                    {
                                                        foreach (var contextDetails in cnxContextDetails.fareCnxInfo.contextDetails)
                                                        {
                                                            Slice_dice_value.Add(new Models.Flight.ResultResponse.spirit_airline()
                                                            {
                                                                leg_ref = leg.ToString(),
                                                                segment_ref = fareContextDetails.requestedSegmentInfo.segRef,
                                                                code = contextDetails,
                                                            });
                                                            leg++;
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        #endregion;

                        List<Models.Flight.ResultResponse.OutboundInbounddata> OutboundInbounddata = new List<Models.Flight.ResultResponse.OutboundInbounddata>();
                        List<string> airlinelists = new List<string>();
                        int index_out_in_bound = 0;

                        string flight_uniqueid = "";

                        foreach (Amadeus_wsdl.ReferencingDetailsType_191583C2 Out_in_bound in Out_in_bound_list)
                        {
                            int leg_index = 1;
                            List<Models.Flight.ResultResponse.flightlist> flightlist = new List<Models.Flight.ResultResponse.flightlist>();

                            Amadeus_wsdl.Fare_MasterPricerTravelBoardSearchReplyFlightIndex Fare_MasterPricerTravelBoardSearchReplyFlightIndex = result.Body.Fare_MasterPricerTravelBoardSearchReply.flightIndex[index_out_in_bound];

                            Amadeus_wsdl.Fare_MasterPricerTravelBoardSearchReplyFlightIndexGroupOfFlights legs = Array.Find(Fare_MasterPricerTravelBoardSearchReplyFlightIndex.groupOfFlights, item => item.propFlightGrDetail.flightProposal[0].@ref == Out_in_bound.refNumber);


                            string previous_leg_datetime = "";
                            int totalminutes = 0;
                            foreach (Amadeus_wsdl.Fare_MasterPricerTravelBoardSearchReplyFlightIndexGroupOfFlightsFlightDetails leg_details in legs.flightDetails)
                            {
                                #region MARKUP AND DISCOUNT
                                if (calculate_markup)
                                {

                                    #region Getting Airline Discount
                                    //Mode  true = B2B , false = B2C
                                    //Origin  true = Algeria , false = other
                                    //discount_type  true = Fixed , false = Percentage
                                    //markup_type true = Fixed , false = Percentage

                                    string airline_code = leg_details.flightInformation.companyId.marketingCarrier;
                                    string airport_code = leg_details.flightInformation.location[0].locationId;
                                    string class_code = recommendation.paxFareProduct[0].fareDetails[0].groupOfFares[0].productInformation.cabinProduct[0].cabin;

                                    //  Models.Admin.airportdata.airlinejsonlist Airline_discount_found = new Models.Admin.airportdata.airlinejsonlist();

                                    #region varibale
                                    Boolean discountallow = false;
                                    double discountperc = 0;
                                    double discountvalue = 0;
                                    double markupvalue = 0;
                                    Boolean ismarkuppercentage = false;
                                    #endregion

                                    //if (dt_airlinemarkup.Rows.Count > 0)
                                    //{
                                    //    foreach (DataRow dr in dt_airlinemarkup.Select("airlinecode='" + airline_code + "'"))
                                    //    {
                                    //        discountperc = dr["discountvalue"].ToString() != null && dr["discountvalue"].ToString() != "" ? Convert.ToDouble(dr["discountvalue"].ToString()) : 0;
                                    //        if (dr["destinationcode"].ToString() == searchcriteria.destinations[0].departureairportcode || dr["destinationcode"].ToString().ToLower() == "xx")
                                    //        {
                                    //            discountallow = true;
                                    //            discountvalue = (baseprice * discountperc) / 100;
                                    //        }
                                    //        markupvalue = dr["markupvalue"].ToString() != null && dr["markupvalue"].ToString() != "" ? Convert.ToDouble(dr["markupvalue"].ToString()) : 0;
                                    //        ismarkuppercentage = dr["markuptype"].ToString().ToLower() == "fixed" ? false : true;

                                    //    }
                                    //}

                                    totalprice = 0;

                                    //#region apply on total price
                                    //#region discount apply
                                    //totalprice = apitotalprice - discountvalue;
                                    //#endregion

                                    //#region markup
                                    //// markup formula:  markupvalue= ((baseprice - discount) + tax) * markuppercentage / 100;

                                    //if(ismarkuppercentage==true)
                                    //{
                                    //    double markupval = ((baseprice - discountvalue) + taxprice)  * markupvalue / 100;

                                    //    double markupapply = ((baseprice - discountvalue) + markupval);
                                    //    totalprice = markupapply + taxprice;
                                    //}
                                    //else
                                    //{
                                    //    totalprice = totalprice + markupvalue;
                                    //}
                                    //#endregion
                                    //#endregion

                                    //foreach (DataRow dr in Dt_airline_discount_markup.Select("code='" + airline_code + "'"))
                                    //{
                                    //    /*Airline_discount_found.discount_type = Convert.ToBoolean(dr["discount_type"].ToString());
                                    //    Airline_discount_found.discount = Convert.ToDouble(dr["discount"].ToString());*/

                                    //    Airline_discount_found.ispercentage = dr["ispercentage"].ToString() != null && dr["ispercentage"].ToString() != "" ? Convert.ToBoolean(dr["ispercentage"].ToString()) : false;
                                    //    Airline_discount_found.markup_percentage = dr["markup_percentage"].ToString() != null && dr["markup_percentage"].ToString() != "" ? Convert.ToDouble(dr["markup_percentage"].ToString()) : 0;

                                    //    Airline_discount_found.isfixed = dr["isfixed"].ToString() != null && dr["isfixed"].ToString() != "" ? Convert.ToBoolean(dr["isfixed"].ToString()) : false;
                                    //    Airline_discount_found.markup_fixed = dr["markup_fixed"].ToString() != null && dr["markup_fixed"].ToString() != "" ? Convert.ToDouble(dr["markup_fixed"].ToString()) : 0;

                                    //    break;
                                    //}
                                    #endregion;

                                    #region TOTAL PRICE
                                    //#region Appling Discount
                                    ///*double discount_amount = 0;
                                    //if (Airline_discount_found.discount > 0)
                                    //{
                                    //    if (Airline_discount_found.discount_type)
                                    //    {
                                    //        discount_amount = Airline_discount_found.discount * (Search_criteria_obj.totaladult + Search_criteria_obj.totalchild + Search_criteria_obj.totalinfant);
                                    //    }
                                    //    else if (Airline_discount_found.discount_type == false && Airline_discount_found.discount != 100)
                                    //    {
                                    //        discount_amount = ((totalprice * Airline_discount_found.discount) / 100);
                                    //    }

                                    //    if (discount_amount > totalprice || discount_amount < 0)
                                    //    {
                                    //        discount_amount = 0;
                                    //    }
                                    //}
                                    //totalprice = totalprice - discount_amount;*/
                                    //#endregion;

                                    //#region Applying Markup
                                    //double markup_amount = 0;
                                    ///*if (Airline_discount_found.markup > 0)
                                    //{*/
                                    //if (Airline_discount_found.isfixed == true)
                                    //{
                                    //    markup_amount = markup_amount + Airline_discount_found.markup_fixed * (Search_criteria_obj.totaladult + Search_criteria_obj.totalchild + Search_criteria_obj.totalinfant);
                                    //}
                                    //if (Airline_discount_found.ispercentage == true && Airline_discount_found.markup_percentage != 100)
                                    //{
                                    //    markup_amount = markup_amount + ((totalprice * Airline_discount_found.markup_percentage) / 100);
                                    //}

                                    //if (markup_amount > totalprice || markup_amount < 0)
                                    //{
                                    //    markup_amount = 0;
                                    //}
                                    ///*}*/

                                    ///*totalprice = totalprice + markup_amount;*/


                                    //if (admin_fixed_allow)
                                    //{
                                    //    markup_amount = markup_amount + (admin_fixed * (Search_criteria_obj.totaladult + Search_criteria_obj.totalchild + Search_criteria_obj.totalinfant));
                                    //}
                                    //if (admin_percentage_allow)
                                    //{
                                    //    markup_amount = markup_amount + ((totalprice * admin_percentage) / 100);
                                    //}

                                    //totalprice = totalprice + markup_amount;
                                    //#endregion;

                                    #endregion;

                                    #region BREAKDOWN
                                    foreach (var breakdown in tarriffinfo)
                                    {

                                        #region apply on total price

                                        #region discount apply

                                        double breakdiscountvalue = 0;

                                        if (discountallow == true)
                                        {
                                            breakdiscountvalue = (breakdown.api_baseprice * discountperc) / 100;

                                            breakdown.net_baseprice = breakdown.api_baseprice - breakdiscountvalue;
                                            breakdown.net_totalprice = breakdown.net_baseprice + breakdown.net_tax;
                                        }

                                        #endregion


                                        #region markup
                                        // markup formula:  markupvalue= ((baseprice - discount) + tax) * markuppercentage / 100;

                                        if (ismarkuppercentage == true)
                                        {
                                            double markupval = ((breakdown.api_baseprice - breakdiscountvalue) + breakdown.api_tax) * markupvalue / 100;

                                            double markupapply = (breakdown.api_baseprice + markupval);

                                            breakdown.net_baseprice = markupapply;
                                            breakdown.net_totalprice = markupapply + breakdown.api_tax;
                                        }
                                        else
                                        {
                                            breakdown.net_baseprice = (breakdown.api_baseprice - breakdiscountvalue) + markupvalue;
                                            breakdown.net_totalprice = breakdown.net_baseprice + breakdown.net_tax;
                                        }

                                        #endregion

                                        #endregion


                                        #region Appling Discount
                                        //double base_price_discount_amount_pax = 0;
                                        //double tax_price_discount_amount_pax = 0;
                                        //double total_price_discount_amount_pax = 0;
                                        //if (Airline_discount_found.discount > 0)
                                        //{
                                        //    if (Airline_discount_found.discount_type)
                                        //    {
                                        //        base_price_discount_amount_pax = Airline_discount_found.discount;
                                        //        total_price_discount_amount_pax = Airline_discount_found.discount;
                                        //    }
                                        //    else if (Airline_discount_found.discount_type == false)
                                        //    {
                                        //        base_price_discount_amount_pax = ((breakdown.net_baseprice * Airline_discount_found.discount) / 100);
                                        //        tax_price_discount_amount_pax = ((breakdown.net_tax * Airline_discount_found.discount) / 100);
                                        //        total_price_discount_amount_pax = ((breakdown.net_totalprice * Airline_discount_found.discount) / 100);
                                        //    }

                                        //    if (base_price_discount_amount_pax > breakdown.net_baseprice || base_price_discount_amount_pax < 0)
                                        //    {
                                        //        base_price_discount_amount_pax = 0;
                                        //        tax_price_discount_amount_pax = 0;
                                        //        total_price_discount_amount_pax = 0;
                                        //    }
                                        //}

                                        //breakdown.net_baseprice = breakdown.net_baseprice - base_price_discount_amount_pax;
                                        //breakdown.net_totalprice = breakdown.net_totalprice - total_price_discount_amount_pax;
                                        //breakdown.net_tax = breakdown.net_tax - tax_price_discount_amount_pax;
                                        #endregion;

                                        #region Applying Markup
                                        //double base_price_markup_amount_pax = 0;
                                        //double tax_price_markup_amount_pax = 0;
                                        //double total_price_markup_amount_pax = 0;
                                        ///*if (Airline_discount_found.markup > 0)
                                        //{
                                        //    if (Airline_discount_found.markup_type)
                                        //    {*/
                                        ////  base_price_markup_amount_pax = Airline_discount_found.markup / total_pax;
                                        //// total_price_markup_amount_pax = Airline_discount_found.markup / total_pax;

                                        ////base_price_markup_amount_pax = Airline_discount_found.markup;
                                        ////total_price_markup_amount_pax = Airline_discount_found.markup;

                                        //base_price_markup_amount_pax = markup_amount;
                                        //total_price_markup_amount_pax = markup_amount;

                                        ///*}
                                        //else if (Airline_discount_found.markup_type == false)
                                        //{
                                        //    base_price_markup_amount_pax = ((breakdown.net_baseprice * Airline_discount_found.markup) / 100);
                                        //    tax_price_markup_amount_pax = ((breakdown.net_tax * Airline_discount_found.markup) / 100);
                                        //    total_price_markup_amount_pax = ((breakdown.net_totalprice * Airline_discount_found.markup) / 100);
                                        //}*/

                                        //if (base_price_markup_amount_pax > breakdown.net_baseprice || base_price_markup_amount_pax < 0)
                                        //{
                                        //    base_price_markup_amount_pax = 0;
                                        //    tax_price_markup_amount_pax = 0;
                                        //    total_price_markup_amount_pax = 0;
                                        //}
                                        ///*}*/
                                        //breakdown.net_baseprice = breakdown.net_baseprice + base_price_markup_amount_pax;
                                        //breakdown.net_totalprice = breakdown.net_totalprice + total_price_markup_amount_pax;
                                        //breakdown.net_tax = breakdown.net_tax + tax_price_markup_amount_pax;

                                        //if (admin_fixed_allow)
                                        //{
                                        //    breakdown.net_baseprice = breakdown.net_baseprice + admin_fixed;
                                        //    breakdown.net_totalprice = breakdown.net_totalprice + admin_fixed;
                                        //}
                                        //if (admin_percentage_allow)
                                        //{
                                        //    breakdown.net_baseprice = breakdown.net_baseprice + ((breakdown.net_baseprice * admin_percentage) / 100);
                                        //    breakdown.net_totalprice = breakdown.net_totalprice + ((breakdown.net_totalprice * admin_percentage) / 100);
                                        //    breakdown.net_tax = breakdown.net_tax + ((breakdown.net_tax * admin_percentage) / 100);
                                        //}
                                        #endregion;

                                        //breakdown.totalprice = Convert.ToDouble((breakdown.net_totalprice * currency_exchangerate).ToString("#0.00"));
                                        //breakdown.baseprice = Convert.ToDouble((breakdown.net_baseprice * currency_exchangerate).ToString("#0.00"));
                                        //breakdown.tax = Convert.ToDouble((breakdown.net_tax * currency_exchangerate).ToString("#0.00"));
                                        //breakdown.per_pax_total_price = Convert.ToDouble((breakdown.net_totalprice * currency_exchangerate).ToString("#0.00"));
                                        breakdown.totalprice = Convert.ToDouble((breakdown.net_totalprice).ToString("#0.00"));
                                        breakdown.baseprice = Convert.ToDouble((breakdown.net_baseprice).ToString("#0.00"));
                                        breakdown.tax = Convert.ToDouble((breakdown.net_tax).ToString("#0.00"));
                                        breakdown.per_pax_total_price = Convert.ToDouble((breakdown.net_totalprice).ToString("#0.00"));
                                        totalprice += breakdown.totalprice;
                                    }
                                    #endregion;

                                    calculate_markup = false;
                                }
                                #endregion;


                                Models.Flight.ResultResponse.Arrivaldeparture Departure = new Models.Flight.ResultResponse.Arrivaldeparture();
                                Models.Flight.ResultResponse.Arrivaldeparture Arrival = new Models.Flight.ResultResponse.Arrivaldeparture();
                                List<Models.Flight.ResultResponse.Checkinbaggage> Checkinbaggage = new List<Models.Flight.ResultResponse.Checkinbaggage>();

                                #region Departure

                                DateTime departure_datetime = DateTime.ParseExact(leg_details.flightInformation.productDateTime.dateOfDeparture + " " + leg_details.flightInformation.productDateTime.timeOfDeparture, "ddMMyy HHmm", CultureInfo.InvariantCulture);

                                Departure.time = departure_datetime.ToString("HH:mm");
                                Departure.Date = departure_datetime.ToString("dd-MM-yyyy");
                                Departure.Datetime = departure_datetime.ToString();
                                Departure.city = leg_details.flightInformation.location[0].locationId;
                                Departure.Iata = leg_details.flightInformation.location[0].locationId;
                                Departure.name = leg_details.flightInformation.location[0].locationId;
                                Departure.Terminal = leg_details.flightInformation.location[0].terminal == null ? "" : leg_details.flightInformation.location[0].terminal;

                                //var departure_airport_obj = airportfilelist.Find(x => x.AirportCode == leg_details.flightInformation.location[0].locationId);
                                //if (departure_airport_obj != null)
                                //{
                                Departure.city = leg_details.flightInformation.location[0].locationId; //departure_airport_obj.City;
                                Departure.name = leg_details.flightInformation.location[0].locationId; //departure_airport_obj.AirportName;
                                //}

                                //if (Airport_Filter.Find(x => x.Iata == Departure.Iata) == null)
                                //{
                                //    Airport_Filter.Add(new pepmytrip_api.Models.Flight.Result.response.AirPortInfo()
                                //    {
                                //        Iata = Departure.Iata,
                                //        City = Departure.city,
                                //        Name = Departure.name,
                                //    });
                                //}

                                #endregion;

                                #region Arrival

                                DateTime arrival_datetime = DateTime.ParseExact(leg_details.flightInformation.productDateTime.dateOfArrival + " " + leg_details.flightInformation.productDateTime.timeOfArrival, "ddMMyy HHmm", CultureInfo.InvariantCulture);

                                Arrival.time = arrival_datetime.ToString("HH:mm");
                                Arrival.Date = arrival_datetime.ToString("dd-MM-yyyy");
                                Arrival.Datetime = arrival_datetime.ToString();
                                Arrival.city = leg_details.flightInformation.location[1].locationId;
                                Arrival.Iata = leg_details.flightInformation.location[1].locationId;
                                Arrival.name = leg_details.flightInformation.location[1].locationId;
                                Arrival.Terminal = leg_details.flightInformation.location[1].terminal == null ? "" : leg_details.flightInformation.location[1].terminal;

                                //var arrival_airport_obj = airportfilelist.Find(x => x.AirportCode == leg_details.flightInformation.location[1].locationId);
                                //if (arrival_airport_obj != null)
                                //{
                                Arrival.city = leg_details.flightInformation.location[1].locationId; //arrival_airport_obj.City;
                                Arrival.name = leg_details.flightInformation.location[1].locationId; //arrival_airport_obj.AirportName;
                                                                                                     // }
                                                                                                     //if (Airport_Filter.Find(x => x.Iata == Arrival.Iata) == null)
                                                                                                     //{
                                                                                                     //    Airport_Filter.Add(new Models.Flight.Result.response.AirPortInfo()
                                                                                                     //    {
                                                                                                     //        Iata = Arrival.Iata,
                                                                                                     //        City = Arrival.city,
                                                                                                     //        Name = Arrival.name,
                                                                                                     //    });
                                                                                                     //}
                                #endregion;

                                #region Airline
                                string Aircraft = "";
                                if (leg_details.flightInformation.productDetail != null && leg_details.flightInformation.productDetail.equipmentType != null && leg_details.flightInformation.productDetail.equipmentType != "")
                                {
                                    Aircraft = leg_details.flightInformation.productDetail.equipmentType;
                                    //if (Amadeus_response_obj.dictionaries != null && Amadeus_response_obj.dictionaries.aircraft != null && Amadeus_response_obj.dictionaries.aircraft.Count != 0)
                                    //{
                                    //    var aircraft_obj = Amadeus_response_obj.dictionaries.aircraft.FirstOrDefault(x => x.Key == Aircraft);
                                    //    Aircraft = aircraft_obj.Value;
                                    //}
                                }


                                Models.Flight.ResultResponse.operating_airline operating_airline = new Models.Flight.ResultResponse.operating_airline();
                                Models.Flight.ResultResponse.operating_airline marketing_airline = new Models.Flight.ResultResponse.operating_airline();

                                if (leg_details.flightInformation.companyId.marketingCarrier != null && leg_details.flightInformation.companyId.marketingCarrier != "")
                                {
                                    if (airlinelists.IndexOf(leg_details.flightInformation.companyId.marketingCarrier) < 0)
                                    {
                                        airlinelists.Add(leg_details.flightInformation.companyId.marketingCarrier);
                                    }
                                    marketing_airline.code = leg_details.flightInformation.companyId.marketingCarrier;
                                    marketing_airline.name = leg_details.flightInformation.companyId.marketingCarrier;
                                    marketing_airline.number = leg_details.flightInformation.flightOrtrainNumber
    ;
                                    //var airline_obj = airlinefilelist.Find(x => x.Airlinecode == leg_details.flightInformation.companyId.marketingCarrier);
                                    //if (airline_obj != null)
                                    //{
                                    //    marketing_airline.name = airline_obj.Airlinename;
                                    //    if (Airline_Filter.Find(x => x.Code == leg_details.flightInformation.companyId.marketingCarrier) == null)
                                    //    {
                                    //        Airline_Filter.Add(new Models.Flight.Result.response.FlightFilter()
                                    //        {
                                    //            Code = leg_details.flightInformation.companyId.marketingCarrier,
                                    //            price = totalprice.ToString("#0"),
                                    //            Value = marketing_airline.name,
                                    //        });
                                    //    }
                                    //    else if (Convert.ToDouble(Airline_Filter.Find(x => x.Code == leg_details.flightInformation.companyId.marketingCarrier).price) > totalprice)
                                    //    {
                                    //        Airline_Filter.Find(x => x.Code == leg_details.flightInformation.companyId.marketingCarrier).price = totalprice.ToString("#0");
                                    //    }
                                    //}
                                }

                                if (leg_details.flightInformation.companyId.operatingCarrier != null && leg_details.flightInformation.companyId.operatingCarrier != null && leg_details.flightInformation.companyId.operatingCarrier != "")
                                {
                                    operating_airline.code = leg_details.flightInformation.companyId.operatingCarrier;
                                    operating_airline.name = leg_details.flightInformation.companyId.operatingCarrier;
                                    operating_airline.number = leg_details.flightInformation.flightOrtrainNumber;
                                    //var airline_obj = airlinefilelist.Find(x => x.Airlinecode == leg_details.flightInformation.companyId.operatingCarrier);
                                    //if (airline_obj != null)
                                    //{
                                    operating_airline.name = leg_details.flightInformation.companyId.operatingCarrier; //airline_obj.Airlinename;
                                    //}
                                }
                                #endregion;

                                string CabinClassText = "";
                                string RBD = "";

                                List<string> RBDLIST = new List<string>();

                                #region Baggage

                                Models.Flight.ResultResponse.pax_fareDetailsBySegment baggage_segement_obj = pax_fareDetailsBySegment.Find(x => x.segmentid == leg_index.ToString());
                                if (baggage_segement_obj != null)
                                {

                                    foreach (Models.Flight.ResultResponse.segment_pax_detail segment_pax_detail in baggage_segement_obj.segment_pax_detail)
                                    {
                                        //if (RBD == "")
                                        //{
                                        //    RBD = segment_pax_detail.class_code;
                                        //}

                                        //if (RBDLIST.FindAll(x => x.ToString() == segment_pax_detail.class_code).Count == 0)
                                        //{
                                        //    RBDLIST.Add(segment_pax_detail.class_code);
                                        //}
                                        //if (CabinClassText == "")
                                        //{
                                        //    CabinClassText = segment_pax_detail.cabin;
                                        //}

                                        var bagdata = Array.Find(Itinerary.referencingDetail, item => item.refQualifier == "B");

                                        var serviceFeesGrp = Array.Find(result.Body.Fare_MasterPricerTravelBoardSearchReply.serviceFeesGrp, x => x.freeBagAllowanceGrp != null);

                                        var bagageobjdata = Array.Find(serviceFeesGrp.freeBagAllowanceGrp, x => x.itemNumberInfo[0].number == bagdata.refNumber);

                                        if (bagageobjdata != null && bagageobjdata.freeBagAllownceInfo != null)
                                        {

                                            //var bagageobjdata = Array.Find(result.Body.Fare_MasterPricerTravelBoardSearchReply.serviceFeesGrp[0].freeBagAllowanceGrp, x => x.itemNumberInfo[0].number == bagdata.refNumber);

                                            if (bagageobjdata != null && bagageobjdata.freeBagAllownceInfo != null)
                                            {
                                                var baggagevalue = bagageobjdata.freeBagAllownceInfo.baggageDetails.freeAllowance + (bagageobjdata.freeBagAllownceInfo.baggageDetails.unitQualifier != null && bagageobjdata.freeBagAllownceInfo.baggageDetails.unitQualifier != "" ? bagageobjdata.freeBagAllownceInfo.baggageDetails.unitQualifier : "PC");

                                                if (baggagevalue == "0K")
                                                {
                                                    baggagevalue = "No Baggage";
                                                }
                                                Checkinbaggage.Add(new Models.Flight.ResultResponse.Checkinbaggage()
                                                {
                                                    Type = segment_pax_detail.paxtype + " " + segment_pax_detail.paxid,
                                                    Value = baggagevalue,
                                                });
                                            }
                                        }
                                        //if (segment_pax_detail.baggage != null && segment_pax_detail.baggage != "")
                                        //{
                                        //    Checkinbaggage.Add(new Models.Flight.Result.response.Checkinbaggage()
                                        //    {
                                        //        Type = segment_pax_detail.paxtype + " " + segment_pax_detail.paxid,
                                        //        Value = segment_pax_detail.baggage,
                                        //    });
                                        //}
                                    }
                                }
                                #endregion;

                                #region rbd class

                                var fareDetailsgroup_obj = fareDetailsgroup.Find(x => x.segmentid == index_out_in_bound);

                                if (fareDetailsgroup_obj != null)
                                {
                                    var gropfare_obj = fareDetailsgroup_obj.groupfare.Find(x => x.groupindex == leg_index);

                                    if (gropfare_obj != null)
                                    {
                                        if (RBD == "")
                                        {
                                            RBD = gropfare_obj.rbd;
                                        }
                                        if (CabinClassText == "")
                                        {
                                            CabinClassText = gropfare_obj.cabin;
                                        }

                                        if (RBDLIST.FindAll(x => x.ToString() == gropfare_obj.rbd).Count == 0)
                                        {
                                            RBDLIST.Add(gropfare_obj.rbd);
                                        }
                                    }
                                }
                                #endregion

                                TimeSpan ts = arrival_datetime.Subtract(departure_datetime);
                                string connectiontime = "";
                                if (previous_leg_datetime == "")
                                {
                                    previous_leg_datetime = arrival_datetime.ToString();
                                }
                                else
                                {
                                    TimeSpan Ts = (departure_datetime - Convert.ToDateTime(previous_leg_datetime));
                                    connectiontime = Convert.ToInt32(Ts.TotalMinutes).ToString();
                                    previous_leg_datetime = arrival_datetime.ToString();

                                    totalminutes = totalminutes + Convert.ToInt32(connectiontime);
                                }

                                totalminutes = totalminutes + Convert.ToInt32(ts.TotalMinutes);

                                string availabilityCnxType_slice_dice = "";

                                if (Slice_dice_value != null && Slice_dice_value.Count > 0)
                                {
                                    var availabilityCnxType_slice_dice_find = Slice_dice_value.Find(x => x.segment_ref == (OutboundInbounddata.Count + 1).ToString() && x.leg_ref == (flightlist.Count + 1).ToString());
                                    if (availabilityCnxType_slice_dice_find != null)
                                    {
                                        availabilityCnxType_slice_dice = availabilityCnxType_slice_dice_find.code;
                                    }
                                }

                                #region technical stop

                                List<Models.Flight.ResultResponse.technicalStop> technicalStop = new List<Models.Flight.ResultResponse.technicalStop>();

                                if (leg_details.technicalStop != null && leg_details.technicalStop.Length > 0)
                                {
                                    foreach (var technical in leg_details.technicalStop)
                                    {
                                        int stop_totalminutes = 0;
                                        string stop_previous_leg_datetime = "";

                                        DateTime depstoptime = new DateTime();
                                        DateTime arrstoptime = new DateTime();

                                        List<Models.Flight.ResultResponse.stopDetails> stopDetails = new List<Models.Flight.ResultResponse.stopDetails>();
                                        if (technical.stopDetails != null && technical.stopDetails.Length > 0)
                                        {

                                            int stopindex = 0;
                                            foreach (var stop in technical.stopDetails)
                                            {
                                                #region stop date time
                                                string stoptime = "";
                                                string stopdate = "";
                                                string stopdatetime = "";
                                                string location_city = "";
                                                string location_Iata = "";
                                                string location_name = "";


                                                if (stop.date != null && stop.date != "" && stop.firstTime != null && stop.firstTime != "")
                                                {
                                                    if (stopindex == 0)
                                                    {
                                                        depstoptime = DateTime.ParseExact(stop.date + " " + stop.firstTime, "ddMMyy HHmm", CultureInfo.InvariantCulture);

                                                    }
                                                    else
                                                    {
                                                        arrstoptime = DateTime.ParseExact(stop.date + " " + stop.firstTime, "ddMMyy HHmm", CultureInfo.InvariantCulture);


                                                    }
                                                    DateTime stop_date_time = DateTime.ParseExact(stop.date + " " + stop.firstTime, "ddMMyy HHmm", CultureInfo.InvariantCulture);
                                                    stoptime = stop_date_time.ToString("HH:mm");
                                                    stopdate = stop_date_time.ToString("dd-MM-yyyy");
                                                    stopdatetime = stop_date_time.ToString();
                                                }
                                                if (stop.locationId != null && stop.locationId != "")
                                                {
                                                    //var location_airport_obj = airportfilelist.Find(x => x.AirportCode == stop.locationId);
                                                    //if (departure_airport_obj != null)
                                                    //{
                                                    location_city = stop.locationId;
                                                    location_name = stop.locationId;
                                                    location_Iata = stop.locationId;
                                                    //}
                                                }

                                                #endregion;

                                                stopDetails.Add(new Models.Flight.ResultResponse.stopDetails()
                                                {
                                                    date = stop.date == null || stop.date == "" ? "" : stopdate,
                                                    dateTime = stopdatetime,
                                                    dateQualifier = stop.dateQualifier == null || stop.dateQualifier == "" ? "" : stop.dateQualifier,
                                                    equipementType = stop.equipementType == null || stop.equipementType == "" ? "" : stop.equipementType,
                                                    firstTime = stop.firstTime == null || stop.firstTime == "" ? "" : stoptime,
                                                    locationId = stop.locationId == null || stop.locationId == "" ? "" : stop.locationId,
                                                    location_city = location_city,
                                                    location_name = location_name,
                                                });
                                                stopindex++;
                                            }
                                        }

                                        TimeSpan tss = arrstoptime.Subtract(depstoptime);
                                        string stopconnectiontime = "";
                                        if (stop_previous_leg_datetime == "")
                                        {
                                            stop_previous_leg_datetime = arrstoptime.ToString();
                                        }
                                        else
                                        {
                                            TimeSpan Ts = (depstoptime - Convert.ToDateTime(stop_previous_leg_datetime));
                                            stopconnectiontime = Convert.ToInt32(Ts.TotalMinutes).ToString();
                                            stop_previous_leg_datetime = arrstoptime.ToString();
                                        }

                                        stop_totalminutes = stop_totalminutes + Convert.ToInt32(tss.TotalMinutes);

                                        technicalStop.Add(new Models.Flight.ResultResponse.technicalStop()
                                        {
                                            stopDetails = stopDetails,
                                            stopconnectiontime = stopconnectiontime,
                                            stop_totalminutes = stop_totalminutes,
                                        });
                                    }
                                }

                                #endregion

                                flight_uniqueid += "|" + Departure.Iata + "-" + Convert.ToDateTime(Departure.Datetime).ToString("dd/MM/yyyy-HH:mm") + "-" + operating_airline.code + "-" + operating_airline.number;

                                flightlist.Add(new Models.Flight.ResultResponse.flightlist()
                                {
                                    Aircraft = Aircraft,
                                    Arrival = Arrival,
                                    Departure = Departure,
                                    CheckinBaggage = Checkinbaggage,
                                    CabinClassText = CabinClassText,
                                    FlightMinutes = Convert.ToInt32(ts.TotalMinutes).ToString(),
                                    FlightTime = ts.ToString(@"hh\:mm"),
                                    MarketingAirline = marketing_airline,
                                    OperatingAirline = operating_airline,
                                    connectiontime = connectiontime,
                                    RBD = RBD,
                                    RBDLIST = RBDLIST,
                                    availabilityCnxType_slice_dice = availabilityCnxType_slice_dice,
                                    technicalStop = technicalStop,
                                });

                                leg_index++;
                            }

                            totaltime = totaltime + totalminutes;

                            OutboundInbounddata.Add(new Models.Flight.ResultResponse.OutboundInbounddata()
                            {
                                flightlist = flightlist,
                                totaltime = totalminutes.ToString(),
                            });

                            index_out_in_bound++;
                        }

                        #region Filter Bind
                        List<string> Inbound_airport = new List<string>();
                        List<string> Outbound_airport = new List<string>();

                        #region outbound filter

                        //stops filter
                        //if (OutboundInbounddata[0].flightlist.Count == 1)
                        //{
                        //    if (Outboundstops.direct == null)
                        //    {
                        //        Outboundstops.direct = totalprice;
                        //    }
                        //    else if (Convert.ToDouble(Outboundstops.direct) > totalprice)
                        //    {
                        //        Outboundstops.direct = totalprice;
                        //    }
                        //}
                        //else if (OutboundInbounddata[0].flightlist.Count == 2)
                        //{
                        //    if (Outboundstops.onestop == null)
                        //    {
                        //        Outboundstops.onestop = totalprice;
                        //    }
                        //    else if (Convert.ToDouble(Outboundstops.onestop) > totalprice)
                        //    {
                        //        Outboundstops.onestop = totalprice;
                        //    }
                        //}
                        //else if (OutboundInbounddata[0].flightlist.Count > 2)
                        //{
                        //    if (Outboundstops.morethanonestop == null)
                        //    {
                        //        Outboundstops.morethanonestop = totalprice;
                        //    }
                        //    else if (Convert.ToDouble(Outboundstops.morethanonestop) > totalprice)
                        //    {
                        //        Outboundstops.morethanonestop = totalprice;
                        //    }
                        //}
                        ////stops filter

                        ////duration filter
                        //if (Outboundstops.journeymin == null)
                        //{
                        //    Outboundstops.journeymin = Convert.ToInt32(OutboundInbounddata[0].totaltime);
                        //}
                        //else if (Convert.ToInt16(Outboundstops.journeymin) > Convert.ToInt16(OutboundInbounddata[0].totaltime))
                        //{
                        //    Outboundstops.journeymin = Convert.ToInt32(OutboundInbounddata[0].totaltime);
                        //}

                        //if (Outboundstops.journeymax == null)
                        //{
                        //    Outboundstops.journeymax = Convert.ToInt32(OutboundInbounddata[0].totaltime);
                        //}
                        //else if (Convert.ToInt16(Outboundstops.journeymax) < Convert.ToInt16(OutboundInbounddata[0].totaltime))
                        //{
                        //    Outboundstops.journeymax = Convert.ToInt32(OutboundInbounddata[0].totaltime);
                        //}
                        ////duration filter

                        //// airport Filter
                        //if (Outbound_Destination_Filter.IndexOf(OutboundInbounddata[0].flightlist[0].Departure.Iata) < 0)
                        //{
                        //    Outbound_Destination_Filter.Add(OutboundInbounddata[0].flightlist[0].Departure.Iata);
                        //}

                        //if (Inbound_Destination_Filter.IndexOf(OutboundInbounddata[0].flightlist[OutboundInbounddata[0].flightlist.Count - 1].Arrival.Iata) < 0)
                        //{
                        //    Inbound_Destination_Filter.Add(OutboundInbounddata[0].flightlist[OutboundInbounddata[0].flightlist.Count - 1].Arrival.Iata);
                        //}

                        //if (Outbound_airport.IndexOf(OutboundInbounddata[0].flightlist[0].Departure.Iata) < 0)
                        //{
                        //    Outbound_airport.Add(OutboundInbounddata[0].flightlist[0].Departure.Iata);
                        //}

                        //if (Inbound_airport.IndexOf(OutboundInbounddata[0].flightlist[OutboundInbounddata[0].flightlist.Count - 1].Arrival.Iata) < 0)
                        //{
                        //    Inbound_airport.Add(OutboundInbounddata[0].flightlist[OutboundInbounddata[0].flightlist.Count - 1].Arrival.Iata);
                        //}


                        // airport Filter


                        #endregion;

                        #region Inbound filter
                        //if (OutboundInbounddata.Count > 1)
                        //{
                        //    //stops filter
                        //    if (OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist.Count == 1)
                        //    {
                        //        if (Inboundstops.direct == null)
                        //        {
                        //            Inboundstops.direct = totalprice;
                        //        }
                        //        else if (Convert.ToDouble(Inboundstops.direct) > totalprice)
                        //        {
                        //            Inboundstops.direct = totalprice;
                        //        }
                        //    }
                        //    else if (OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist.Count == 2)
                        //    {
                        //        if (Inboundstops.onestop == null)
                        //        {
                        //            Inboundstops.onestop = totalprice;
                        //        }
                        //        else if (Convert.ToDouble(Inboundstops.onestop) > totalprice)
                        //        {
                        //            Inboundstops.onestop = totalprice;
                        //        }
                        //    }
                        //    else if (OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist.Count > 2)
                        //    {
                        //        if (Inboundstops.morethanonestop == null)
                        //        {
                        //            Inboundstops.morethanonestop = totalprice;
                        //        }
                        //        else if (Convert.ToDouble(Inboundstops.morethanonestop) > totalprice)
                        //        {
                        //            Inboundstops.morethanonestop = totalprice;
                        //        }
                        //    }
                        //    //stops filter

                        //    //duration filter
                        //    if (Inboundstops.journeymin == null)
                        //    {
                        //        Inboundstops.journeymin = Convert.ToInt32(OutboundInbounddata[OutboundInbounddata.Count - 1].totaltime);
                        //    }
                        //    else if (Convert.ToInt16(Inboundstops.journeymin) > Convert.ToInt32(OutboundInbounddata[OutboundInbounddata.Count - 1].totaltime))
                        //    {
                        //        Inboundstops.journeymin = Convert.ToInt32(OutboundInbounddata[OutboundInbounddata.Count - 1].totaltime);
                        //    }

                        //    if (Inboundstops.journeymax == null)
                        //    {
                        //        Inboundstops.journeymax = Convert.ToInt32(OutboundInbounddata[OutboundInbounddata.Count - 1].totaltime);
                        //    }
                        //    else if (Convert.ToInt16(Inboundstops.journeymax) < Convert.ToInt16(OutboundInbounddata[OutboundInbounddata.Count - 1].totaltime))
                        //    {
                        //        Inboundstops.journeymax = Convert.ToInt32(OutboundInbounddata[OutboundInbounddata.Count - 1].totaltime);
                        //    }
                        //    //duration filter


                        //    // airport Filter
                        //    if (Inbound_Destination_Filter.IndexOf(OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist[0].Departure.Iata) < 0)
                        //    {
                        //        Inbound_Destination_Filter.Add(OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist[0].Departure.Iata);
                        //    }

                        //    if (Outbound_Destination_Filter.IndexOf(OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist[OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist.Count - 1].Arrival.Iata) < 0)
                        //    {
                        //        Outbound_Destination_Filter.Add(OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist[OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist.Count - 1].Arrival.Iata);
                        //    }


                        //    if (Inbound_airport.IndexOf(OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist[0].Departure.Iata) < 0)
                        //    {
                        //        Inbound_airport.Add(OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist[0].Departure.Iata);
                        //    }

                        //    if (Outbound_airport.IndexOf(OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist[OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist.Count - 1].Arrival.Iata) < 0)
                        //    {
                        //        Outbound_airport.Add(OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist[OutboundInbounddata[OutboundInbounddata.Count - 1].flightlist.Count - 1].Arrival.Iata);
                        //    }

                        //}

                        #endregion;

                        #endregion;

                        double net_totalprice = totalprice;
                        //double price_total = totalprice * currency_exchangerate;
                        double price_total = totalprice;

                        string guid = System.Guid.NewGuid().ToString() + System.Guid.NewGuid().ToString() + result_index;
                        //   Itinerary.refrence = guid;
                        flight_uniqueids.Add(flight_uniqueid);

                        #region Matrix Airline Filter
                        //if (airlinelists.Count == 1)
                        //{
                        //    var flight_matric = FlightFilter_price.Find(x => x.Code == airlinelists[0]);
                        //    if (flight_matric == null)
                        //    {
                        //        double airline_matric_price_direct = 0;
                        //        double airline_matric_price_stop = 0;
                        //        if ((OutboundInbounddata.Count == 1 && OutboundInbounddata[0].flightlist.Count == 1) || (OutboundInbounddata.Count == 2 && OutboundInbounddata[0].flightlist.Count == 1 && OutboundInbounddata[1].flightlist.Count == 1))
                        //        {
                        //            airline_matric_price_direct = net_totalprice;
                        //        }

                        //        if ((OutboundInbounddata.Count == 1 && OutboundInbounddata[0].flightlist.Count != 1) || (OutboundInbounddata.Count == 2 && OutboundInbounddata[0].flightlist.Count != 1 && OutboundInbounddata[1].flightlist.Count != 1))
                        //        {
                        //            airline_matric_price_stop = net_totalprice;
                        //        }

                        //        var airline_obj = airlinefilelist.Find(x => x.Airlinecode == airlinelists[0]);
                        //        FlightFilter_price.Add(new Models.Flight.Result.response.FlightFilter_price()
                        //        {
                        //            Code = airlinelists[0],
                        //            Value = airline_obj.Airlinename,
                        //            direct = airline_matric_price_direct,
                        //            stops = airline_matric_price_stop,
                        //        });
                        //    }
                        //    else
                        //    {
                        //        double airline_matric_price_direct = flight_matric.direct;
                        //        double airline_matric_price_stop = flight_matric.stops;
                        //        if ((OutboundInbounddata.Count == 1 && OutboundInbounddata[0].flightlist.Count == 1) || (OutboundInbounddata.Count == 2 && OutboundInbounddata[0].flightlist.Count == 1 && OutboundInbounddata[1].flightlist.Count == 1))
                        //        {
                        //            airline_matric_price_direct = net_totalprice;
                        //        }

                        //        if ((OutboundInbounddata.Count == 1 && OutboundInbounddata[0].flightlist.Count != 1) || (OutboundInbounddata.Count == 2 && OutboundInbounddata[0].flightlist.Count != 1 && OutboundInbounddata[1].flightlist.Count != 1))
                        //        {
                        //            airline_matric_price_stop = net_totalprice;
                        //        }


                        //        flight_matric.direct = (flight_matric.direct == 0 || flight_matric.direct > airline_matric_price_direct) ? airline_matric_price_direct : flight_matric.direct;

                        //        flight_matric.stops = (flight_matric.stops == 0 || flight_matric.stops > airline_matric_price_stop) ? airline_matric_price_stop : flight_matric.stops;


                        //    }
                        //}

                        //List<string> distinct_airline = airlinelists.Distinct().ToList();
                        //foreach (var airline_code in distinct_airline)
                        //{
                        //    var flight_matric = FlightFilter_price.Find(x => x.Code == airline_code);
                        //    if (flight_matric == null)
                        //    {
                        //        double airline_matric_price_direct = 0;
                        //        double airline_matric_price_stop = 0;
                        //        if ((OutboundInbounddata.Count == 1 && OutboundInbounddata[0].flightlist.Count == 1) || (OutboundInbounddata.Count == 2 && OutboundInbounddata[0].flightlist.Count == 1 && OutboundInbounddata[1].flightlist.Count == 1))
                        //        {
                        //            airline_matric_price_direct = net_totalprice;
                        //        }

                        //        if ((OutboundInbounddata.Count == 1 && OutboundInbounddata[0].flightlist.Count != 1) || (OutboundInbounddata.Count == 2 && OutboundInbounddata[0].flightlist.Count != 1 && OutboundInbounddata[1].flightlist.Count != 1))
                        //        {
                        //            airline_matric_price_stop = net_totalprice;
                        //        }

                        //        var airline_obj = airlinefilelist.Find(x => x.Airlinecode == airline_code);

                        //        string airlinename = airline_code;
                        //        if (airline_obj != null)
                        //        {
                        //            airlinename = airline_obj.Airlinename;
                        //        }

                        //        FlightFilter_price.Add(new Models.Flight.Result.response.FlightFilter_price()
                        //        {
                        //            Code = airline_code,
                        //            Value = airlinename,
                        //            direct = airline_matric_price_direct,
                        //            stops = airline_matric_price_stop,
                        //        });
                        //    }
                        //    else
                        //    {
                        //        double airline_matric_price_direct = flight_matric.direct;
                        //        double airline_matric_price_stop = flight_matric.stops;
                        //        if ((OutboundInbounddata.Count == 1 && OutboundInbounddata[0].flightlist.Count == 1) || (OutboundInbounddata.Count == 2 && OutboundInbounddata[0].flightlist.Count == 1 && OutboundInbounddata[1].flightlist.Count == 1))
                        //        {
                        //            airline_matric_price_direct = net_totalprice;
                        //        }

                        //        if ((OutboundInbounddata.Count == 1 && OutboundInbounddata[0].flightlist.Count != 1) || (OutboundInbounddata.Count == 2 && OutboundInbounddata[0].flightlist.Count != 1 && OutboundInbounddata[1].flightlist.Count != 1))
                        //        {
                        //            airline_matric_price_stop = net_totalprice;
                        //        }


                        //        flight_matric.direct = (flight_matric.direct == 0 || flight_matric.direct > airline_matric_price_direct) ? airline_matric_price_direct : flight_matric.direct;

                        //        flight_matric.stops = (flight_matric.stops == 0 || flight_matric.stops > airline_matric_price_stop) ? airline_matric_price_stop : flight_matric.stops;


                        //    }
                        //}
                        #endregion;

                        string faretype = "";
                        string faretypename = "";

                        List<Models.Flight.ResultResponse.familylistdata> farefamilylistdata = new List<Models.Flight.ResultResponse.familylistdata>();

                        if (recommendation.fareFamilyRef != null && Array.FindAll(recommendation.fareFamilyRef, x => x.refQualifier == "F").Length > 0)
                        {
                            string refNumber = Array.Find(recommendation.fareFamilyRef, x => x.refQualifier == "F").refNumber;


                            string farematchno = Array.Find(recommendation.fareFamilyRef, x => x.refQualifier != "F") != null ? Array.Find(recommendation.fareFamilyRef, x => x.refQualifier != "F").refNumber : "";

                            if (farematchno == null || farematchno == "")
                            {
                                farematchno = "";
                            }

                            var faretypeobj = Array.Find(result.Body.Fare_MasterPricerTravelBoardSearchReply.familyInformation, x => x.refNumber == refNumber);

                            if (faretypeobj == null)
                            {
                                goto fareend;
                            }
                            faretype = faretypeobj.fareFamilyname;
                            faretypename = faretypeobj.description;

                            string carrier = faretypeobj.carrier;

                            var familylist = Array.FindAll(result.Body.Fare_MasterPricerTravelBoardSearchReply.familyInformation, x => x.carrier == carrier);

                            if (familylist != null && familylist.Length > 0)
                            {
                                //  faretypeobj.services[0].reference
                                //var matchdata = Array.FindAll(familylist, x => x.services != null && x.services.Length > 0 && Array.FindAll(x.services, y => y.reference == farematchno).Length > 0);

                                int matchCount = 0;
                                foreach (var otherServices in familylist)
                                {
                                    if (faretypeobj.services != null && faretypeobj.services.Length > 0)
                                    {
                                        foreach (var ref1Service in faretypeobj.services)
                                        {
                                            if (otherServices.services != null && otherServices.services.Length > 0)
                                            {
                                                foreach (var otherService in otherServices.services)
                                                {
                                                    if (ref1Service.reference == otherService.reference &&
                            ref1Service.status == otherService.status)
                                                    {

                                                        if (farefamilylistdata.FindAll(x => x.code.ToString() == otherServices.fareFamilyname).Count == 0)
                                                        {
                                                            farefamilylistdata.Add(new Models.Flight.ResultResponse.familylistdata()
                                                            {
                                                                code = otherServices.fareFamilyname,
                                                                name = otherServices.description,
                                                            });
                                                        }
                                                        matchCount++;
                                                    }
                                                }
                                            }

                                        }
                                    }

                                }

                                //if (matchdata.Length > 0)
                                //{
                                //    foreach (var match in matchdata)
                                //    {
                                //        farefamilylistdata.Add(match.fareFamilyname);
                                //    }
                                //}
                            }
                        fareend:;
                        }

                        Models.Flight.ResultResponse.price price = new Models.Flight.ResultResponse.price();

                        price.currency = currency;
                        price.currency_sign = currency;
                        price.base_price = net_totalprice;
                        price.total_price = price_total;
                        price.tarriffinfo = tarriffinfo;


                        Flightdata.Add(new Models.Flight.ResultResponse.Flightdata()
                        {
                            //Airlinelists = airlinelists,
                            //Arrivalcityairports = Inbound_airport,
                            //Departurecityairports = Outbound_airport,
                            faretype = faretype,
                            faretypename = faretypename,
                            id = guid,
                            price = price,
                            pcc = _configuration["FlightSettings:AirOfficeID"],
                            Offercode = guid,
                            unique_id = flight_uniqueid,
                            searchindex = 0,
                            api_response_ref = recommendation.itemNumber.itemNumberId.number,
                            // refundable = 1,
                            supplier = (int)SupplierEnum.Amadeus,
                            totaltime = totaltime,
                            OutboundInboundlist = OutboundInbounddata,
                            farefamilylistdata = farefamilylistdata
                        });
                        result_index++;
                    }
                }
                #endregion;


                List<string> flight_unique_ids = new List<string>();
                List<com.ThirdPartyAPIs.Models.Flight.ResultResponse.Flightdata> Flight_Data_new = new List<Models.Flight.ResultResponse.Flightdata>();

                flight_unique_ids = flight_uniqueids.Distinct().ToList();
                Flightdata = Flightdata.OrderBy(x => x.price.total_price).ToList();

                foreach (var flight_unique_id in flight_unique_ids)
                {
                    var flight_find = Flightdata.FindAll(x => x.unique_id == flight_unique_id);

                    if (flight_find.Count > 1)
                    {
                        string asasas = "";
                    }
                    // Flight_Data_new.AddRange(flight_find);

                    var flight_find_obj = flight_find.OrderBy(x => x.searchindex).FirstOrDefault();
                    Flight_Data_new.Add(flight_find_obj);
                }

                //List<com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightFilter> Airline_Filter_new = new List<com.ThirdPartyAPIs..Models.Flight.Result.response.FlightFilter>();
                //List< com.ThirdPartyAPIs..Models.Flight.Result.response.AirPortInfo> Airport_Filter_new = new List<com.ThirdPartyAPIs..Models.Flight.Result.response.AirPortInfo>();

                //List<com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightFilter_price> FlightFilter_price_new = new List<com.ThirdPartyAPIs..Models.Flight.ResultResponse.FlightFilter_price>();

                List<string> Outbound_Destination_Filter_new = new List<string>();
                List<string> Inbound_Destination_Filter_new = new List<string>();
                //com.ThirdPartyAPIs.Models.Flight.ResultResponse.stopsdata Outboundstops_new = new com.ThirdPartyAPIs.Models.Flight.ResultResponse.stopsdata();
                //com.ThirdPartyAPIs.Models.Flight.ResultResponse.stopsdata Inboundstops_new = new com.ThirdPartyAPIs.Models.Flight.ResultResponse.stopsdata();

                //com.ThirdPartyAPIs.Models.Flight.Result.response.flexDate flexDate = new Models.Flight.Result.response.flexDate();

                //List<com.ThirdPartyAPIs.Models.Flight.Result.response.flexdata> flexdata = new List<Models.Flight.Result.response.flexdata>();

                com.ThirdPartyAPIs.Models.Flight.ResultResponse.FlightResponse newdata = new Models.Flight.ResultResponse.FlightResponse();

                newdata.flight_unique_ids = flight_unique_ids;
                newdata.totalflight = Flight_Data_new.Count;
                newdata.Currency = currency;
                newdata.Data = Flight_Data_new;
                newdata.sc = SC;
                newdata.totaladult = data.adults;
                newdata.totalchild = data.children;
                newdata.totalinfant = data.infant;
                newdata.success = true;
                //flightdatamain.Data = newdata;
                flightdatamain = newdata;

                if (!string.IsNullOrWhiteSpace(xmlfiles))
                {
                    //Directory.CreateDirectory(xmlfiles);
                    //File.WriteAllText(Path.Combine(xmlfiles, Token + "_Flight_amadeus_request.xml"), requestContent, Encoding.UTF8);
                    //File.WriteAllText(Path.Combine(xmlfiles, "_Flight_amadeus_response.xml"), re ?? string.Empty, Encoding.UTF8);

                    System.IO.File.WriteAllText(xmlfiles + SC + "flightavailable_secret.json", System.Text.Json.JsonSerializer.Serialize(result), System.Text.ASCIIEncoding.UTF8);

                    //System.IO.File.WriteAllText(xmlfiles + Token + "_flight_result_Amadeus.json", Newtonsoft.Json.JsonConvert.SerializeObject(flightdatamain), System.Text.ASCIIEncoding.UTF8);

                    System.IO.File.WriteAllText(xmlfiles + SC + "_flight_result_Amadeus.json", System.Text.Json.JsonSerializer.Serialize(flightdatamain), System.Text.ASCIIEncoding.UTF8);


                    System.IO.File.WriteAllText(xmlfiles + SC + "_flight_result.json", System.Text.Json.JsonSerializer.Serialize(flightdatamain), System.Text.ASCIIEncoding.UTF8);
                }

                #endregion

                #endregion

                return flightdatamain;
            }
            catch (Exception ex)
            {
                //SaveAmadeusLog("", "", ex.Message + " /n error Fare_MasterPricerTravelBoardSearch");
                //return [.. concurrentPricing];
                return flightdatamain;
            }

        }

        public async Task<ResultResponse.FlightResponse> Fare_PriceUpsellWithoutPNR(ResultResponse.Flightdata Selected_flight_result, string sc,int totaladult, int totalchild, int totalinfant)
        {
            var flightdatamain = new Models.Flight.ResultResponse.FlightResponse();

            #region Request
            string password = "U9MbJZjzR^EP";

            var url = _configuration["FlightSettings:AirProductionURL"];
            Guid guid = Guid.NewGuid();
            string guidString = guid.ToString();
            byte[] nonce = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(nonce);
            }
            DateTime timestamp = DateTime.UtcNow;
            string formattedTimestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss+00:00");
            string encodedNonce = Convert.ToBase64String(nonce);
            string passSHA = "";
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] passwordHash = sha1.ComputeHash(passwordBytes);

                byte[] timestampBytes = Encoding.ASCII.GetBytes(formattedTimestamp);
                byte[] combinedBytes = new byte[nonce.Length + timestampBytes.Length + passwordHash.Length];

                Buffer.BlockCopy(nonce, 0, combinedBytes, 0, nonce.Length);
                Buffer.BlockCopy(timestampBytes, 0, combinedBytes, nonce.Length, timestampBytes.Length);
                Buffer.BlockCopy(passwordHash, 0, combinedBytes, nonce.Length + timestampBytes.Length, passwordHash.Length);

                byte[] passSHABytes = sha1.ComputeHash(combinedBytes);
                passSHA = Convert.ToBase64String(passSHABytes);
            }
            StringBuilder dbps = new();
            dbps.Append("<passengersGroup>");
            dbps.Append("<segmentRepetitionControl>");
            dbps.Append("<segmentControlDetails>");
            dbps.Append("<quantity>1</quantity>");
            dbps.Append("<numberOfUnits>" + totaladult + "</numberOfUnits>");
            dbps.Append("</segmentControlDetails>");
            dbps.Append("</segmentRepetitionControl>");
            dbps.Append("<travellersID>");
            for (int i = 0; i < totaladult; i++)
            {
                dbps.Append("<travellerDetails>");
                dbps.Append("<measurementValue>" + (i + 1) + "</measurementValue>");
                dbps.Append("</travellerDetails>");
            }
            dbps.Append("</travellersID>");
            dbps.Append("<discountPtc>");
            dbps.Append("<valueQualifier>ADT</valueQualifier>");
            dbps.Append("</discountPtc>");
            dbps.Append("</passengersGroup>");
            if (totalchild > 0)
            {
                dbps.Append("<passengersGroup>");
                dbps.Append("<segmentRepetitionControl>");
                dbps.Append("<segmentControlDetails>");
                dbps.Append("<quantity>2</quantity>");
                dbps.Append("<numberOfUnits>" + totalchild + "</numberOfUnits>");
                dbps.Append("</segmentControlDetails>");
                dbps.Append("</segmentRepetitionControl>");
                dbps.Append("<travellersID>");
                for (int i = 0; i < totalchild; i++)
                {
                    dbps.Append("<travellerDetails>");
                    dbps.Append("<measurementValue>" + (totaladult + i + 1) + "</measurementValue>");
                    dbps.Append("</travellerDetails>");
                }
                dbps.Append("</travellersID>");
                dbps.Append("<discountPtc>");
                dbps.Append("<valueQualifier>CNN</valueQualifier>");
                dbps.Append("</discountPtc>");
                dbps.Append("</passengersGroup>");
            }
            if (totalinfant > 0)
            {
                dbps.Append("<passengersGroup>");
                dbps.Append("<segmentRepetitionControl>");
                dbps.Append("<segmentControlDetails>");
                dbps.Append("<quantity>3</quantity>");
                dbps.Append("<numberOfUnits>" + totalinfant + "</numberOfUnits>");
                dbps.Append("</segmentControlDetails>");
                dbps.Append("</segmentRepetitionControl>");
                dbps.Append("<travellersID>");
                for (int i = 0; i < totalinfant; i++)
                {
                    dbps.Append("<travellerDetails>");
                    dbps.Append("<measurementValue>" + (i + 1) + "</measurementValue>");
                    dbps.Append("</travellerDetails>");
                }
                dbps.Append("</travellersID>");
                dbps.Append("<discountPtc>");
                dbps.Append("<valueQualifier>INF</valueQualifier>");
                dbps.Append("<fareDetails>");
                dbps.Append("<qualifier>766</qualifier>");
                dbps.Append("</fareDetails>");
                dbps.Append("</discountPtc>");
                dbps.Append("</passengersGroup>");
            }
            
            StringBuilder sb = new();

            int flightIndicator = 1;
            int itemNumber = 1;
            int itemNumberindex = 0;
            string carrierInformation_otherCompany = "";

            foreach (var OutboundInbounddata in Selected_flight_result.OutboundInboundlist)
            {
                string FlightIndicator_str = flightIndicator.ToString();

                foreach (var flightlist in OutboundInbounddata.flightlist)
                {
                    carrierInformation_otherCompany = flightlist.MarketingAirline.code;

                    string RBD = flightlist.RBD;

                    sb.AppendLine("  <segmentGroup><segmentInformation><flightDate><departureDate>" + Convert.ToDateTime(flightlist.Departure.Datetime).ToString("ddMMyy") + "</departureDate><departureTime>" + Convert.ToDateTime(flightlist.Departure.Datetime).ToString("HHmm") + "</departureTime><arrivalDate>" + Convert.ToDateTime(flightlist.Arrival.Datetime).ToString("ddMMyy") + "</arrivalDate><arrivalTime>" + Convert.ToDateTime(flightlist.Arrival.Datetime).ToString("HHmm") + "</arrivalTime></flightDate><boardPointDetails><trueLocationId>" + flightlist.Departure.Iata + "</trueLocationId></boardPointDetails><offpointDetails><trueLocationId>" + flightlist.Arrival.Iata + "</trueLocationId></offpointDetails><companyDetails><marketingCompany>" + flightlist.MarketingAirline.code + "</marketingCompany><operatingCompany>" + flightlist.OperatingAirline.code + "</operatingCompany></companyDetails><flightIdentification><flightNumber>" + flightlist.MarketingAirline.number + "</flightNumber><bookingClass>" + RBD + "</bookingClass></flightIdentification><flightTypeDetails><flightIndicator>" + FlightIndicator_str + "</flightIndicator></flightTypeDetails><itemNumber>" + itemNumber + "</itemNumber></segmentInformation></segmentGroup> ");
                    itemNumber++;

                    //sb.Append("<segmentGroup>");
                    //sb.Append("<segmentInformation>");
                    //sb.Append("<flightDate>");
                    //sb.Append("<departureDate>" + data.depDt.ToString("ddMMyy") + "</departureDate>");
                    //sb.Append("<departureTime>" + data.departureTime.Replace(":", "") + "</departureTime>");
                    //sb.Append("<arrivalDate>" + data.arrDt.ToString("ddMMyy") + "</arrivalDate>");
                    //sb.Append("<arrivalTime>" + data.arrivalTime.Replace(":", "") + "</arrivalTime>");
                    //sb.Append("</flightDate>");
                    //sb.Append("<boardPointDetails>");
                    //sb.Append("<trueLocationId>" + data.depCode.Split(',')[0] + "</trueLocationId>");
                    //sb.Append("</boardPointDetails>");
                    //sb.Append("<offpointDetails>");
                    //sb.Append("<trueLocationId>" + data.arrCode.Split(',')[0] + "</trueLocationId>");
                    //sb.Append("</offpointDetails>");
                    //sb.Append("<companyDetails>");
                    //sb.Append("<marketingCompany>" + data.marketingCompany.Split('|')[0] + "</marketingCompany>");
                    //sb.Append("<operatingCompany></operatingCompany>");
                    //sb.Append("</companyDetails>");
                    //sb.Append("<flightIdentification>");
                    //sb.Append("<flightNumber>" + data.flightNumber + "</flightNumber>");
                    //sb.Append("<bookingClass>" + data.bookingClass + "</bookingClass>");
                    //sb.Append("</flightIdentification>");
                    //sb.Append("<flightTypeDetails>");
                    //sb.Append("<flightIndicator>1</flightIndicator>");
                    //sb.Append("</flightTypeDetails>");
                    //sb.Append("<itemNumber>" + seg + "</itemNumber>");
                    //sb.Append("</segmentInformation>");
                    //sb.Append("</segmentGroup>");
                }
                itemNumberindex++;
                flightIndicator++;
            }
            
            sb.Append("<pricingOptionGroup>");
            sb.Append("<pricingOptionKey>");
            sb.Append("<pricingOptionKey>VC</pricingOptionKey>");
            sb.Append("</pricingOptionKey>");
            sb.Append("<carrierInformation>");
            sb.Append("<companyIdentification>");
            sb.Append("<otherCompany>" + carrierInformation_otherCompany + "</otherCompany>");
            sb.Append("</companyIdentification>");
            sb.Append("</carrierInformation>");
            sb.Append("</pricingOptionGroup>");
            sb.Append("<pricingOptionGroup>");
            sb.Append("<pricingOptionKey>");
            sb.Append("<pricingOptionKey>RP</pricingOptionKey>");
            sb.Append("</pricingOptionKey>");
            sb.Append("</pricingOptionGroup>");
            sb.Append("<pricingOptionGroup>");
            sb.Append("<pricingOptionKey>");
            sb.Append("<pricingOptionKey>RU</pricingOptionKey>");
            sb.Append("</pricingOptionKey>");
            sb.Append("</pricingOptionGroup>");
            sb.Append("<pricingOptionGroup>");
            sb.Append("<pricingOptionKey>");
            sb.Append("<pricingOptionKey>RLO</pricingOptionKey>");
            sb.Append("</pricingOptionKey>");
            sb.Append("</pricingOptionGroup>");
            sb.Append("<pricingOptionGroup>");
            sb.Append("<pricingOptionKey>");
            sb.Append("<pricingOptionKey>FFH</pricingOptionKey>");
            sb.Append("</pricingOptionKey>");
            sb.Append("</pricingOptionGroup>");
            //sb.Append("<pricingOptionGroup>");
            //sb.Append("<pricingOptionKey>");
            //sb.Append("<pricingOptionKey>PFH</pricingOptionKey>");
            //sb.Append("</pricingOptionKey>");
            //sb.Append("</pricingOptionGroup>");
            sb.Append("</Fare_PriceUpsellWithoutPNR>");

            var content = new StringContent("<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\""
                + " xmlns:awsse=\"http://xml.amadeus.com/2010/06/Session_v3\""
                + " xmlns:sec=\"http://xml.amadeus.com/2010/06/Security_v1\""
                + " xmlns:typ=\"http://xml.amadeus.com/2010/06/Types_v1\""
                + " xmlns:app=\"http://xml.amadeus.com/2010/06/AppMdw_CommonTypes_v3\">"
                + "<s:Header>"
                + "<a:MessageID xmlns:a=\"http://www.w3.org/2005/08/addressing\">" + guidString + "</a:MessageID>"
                + "<a:Action xmlns:a=\"http://www.w3.org/2005/08/addressing\">" + _configuration["FlightSettings:AirSoapAction"] + "TIUNRQ_23_1_1A</a:Action>"
                + "<a:To xmlns:a=\"http://www.w3.org/2005/08/addressing\">" + _configuration["FlightSettings:AirProductionURL"] + "</a:To>"
                + "<Security xmlns=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\">"
                + "<oas:UsernameToken xmlns:oas=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\""
                + " xmlns:oas1=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd\" oas1:Id=\"UsernameToken-1\">"
                + "<oas:Username>" + _configuration["FlightSettings:AirUserName"] + "</oas:Username>"
                + "<oas:Nonce EncodingType=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary\">" + encodedNonce + "</oas:Nonce>"
                + "<oas:Password Type=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest\">" + passSHA + "</oas:Password>"
                + "<oas1:Created>" + formattedTimestamp + "</oas1:Created>"
                + "</oas:UsernameToken>"
                + "</Security>"
                + "<h:AMA_SecurityHostedUser xmlns:h=\"http://xml.amadeus.com/2010/06/Security_v1\">"
                + "<h:UserID POS_Type=\"1\" PseudoCityCode=\"" + _configuration["FlightSettings:AirOfficeID"] + "\" AgentDutyCode=\"" + _configuration["FlightSettings:AirDutyCode"]
                + "\" RequestorType=\"U\"/>"
                + "</h:AMA_SecurityHostedUser>"
                + "<awsse:Session TransactionStatusCode=\"Start\" xmlns:awsse=\"http://xml.amadeus.com/2010/06/Session_v3\"/>"
                + "</s:Header>"
                + "<s:Body>"
                + "<Fare_PriceUpsellWithoutPNR>"
                + dbps.ToString()
                + sb.ToString()
                + "</s:Body>"
                + "</s:Envelope>", null, "application/xml");

            #endregion Request

            var reqt = new HttpRequestMessage(HttpMethod.Post, url);
            reqt.Headers.Add("soapAction", _configuration["FlightSettings:AirSoapAction"] + "TIUNRQ_23_1_1A");
            reqt.Content = content;
            var requestContent = content.ReadAsStringAsync().Result;
            //SaveAmadeusLog(requestContent, "-", "Request Fare_PriceUpsellWithoutPNR");
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            var response = await _httpClient.SendAsync(reqt);
            var re = response.Content.ReadAsStringAsync().Result;

            var xmlfiles = Path.Combine(_env.ContentRootPath, "XmlFiles/");
            if (!string.IsNullOrWhiteSpace(xmlfiles))
            {
                //if (!Directory.Exists(xmlfiles))
                //    Directory.CreateDirectory(xmlfiles);
                //File.WriteAllText(Path.Combine(xmlfiles, Selected_flight_result.id + "_Fare_PriceUpsellWithoutPNR_Request.xml"), requestContent, Encoding.UTF8);
                //File.WriteAllText(Path.Combine(xmlfiles, Selected_flight_result.id + "_Fare_PriceUpsellWithoutPNR_Response.xml"), re ?? string.Empty, Encoding.UTF8);
            }

            return flightdatamain;
        }

    }
}