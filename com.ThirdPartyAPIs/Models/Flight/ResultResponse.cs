using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static com.ThirdPartyAPIs.Models.Flight.ResultResponse;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace com.ThirdPartyAPIs.Models.Flight
{
    public class ResultResponse
    {
        public class FlightResponse
        {
            public bool success { get; set; }
            public bool complete { get; set; }
            public string message { get; set; }
            public int totalflight { get; set; }
            public string sc { get; set; }
            public string Currency { get; set; }
            public int totaladult { get; set; }
            public int totalchild { get; set; }
            public int totalinfant { get; set; }
            public List<string> flight_unique_ids { get; set; }
            public List<Flightdata> Data { get; set; }

        }

        public class Flightdata
        {
            public string faretype { get; set; }
            public string faretypename { get; set; }
            public int supplier { get; set; }
            //public List<string> Airlinelists { get; set; }
            //public List<string> Departurecityairports { get; set; }
            //public List<string> Arrivalcityairports { get; set; }
            //public List<string> baggagelist { get; set; }
            public string pcc { get; set; }
            public string Offercode { get; set; }
            public string ItineraryId { get; set; }
            public string dealcode { get; set; }
            public int dealcount { get; set; }
            public string id { get; set; }
            public string unique_id { get; set; }
            public string api_response_ref { get; set; }
            public int totaltime { get; set; }
            public int searchindex { get; set; }
            public price price { get; set; }
            public List<Connection> Connection { get; set; }
            public List<OutboundInbounddata> OutboundInboundlist { get; set; }

            public List<familylistdata> farefamilylistdata { get; set; }
        }
        public class familylistdata
        {
            public string code { get; set; }
            public string name { get; set; }
        }

        public class Connection
        {
            public string index { get; set; }
            public string refrence { get; set; }
        }
        public class price
        {
            public List<tarriffinfo> tarriffinfo { get; set; }
            public double base_price { get; set; }
            public double tax_price { get; set; }
            public double total_price { get; set; }
            public double airlinemarkuptotal { get; set; }
            public double suppliermarkuptotal { get; set; }
            public double generalmarkuptotal { get; set; }
            public string currency { get; set; }
            public string currency_sign { get; set; }
        }
        public class tarriffinfo
        {
            public double seat_price { get; set; }
            public double net_seat_price { get; set; }
            public double api_seat_price { get; set; }
            public double per_pax_total_price { get; set; }
            public double totalprice { get; set; }
            public double baseprice { get; set; }
            public double tax { get; set; }
            public string currency { get; set; }
            public string paxid { get; set; }
            public int quantity { get; set; }
            public string paxtype_text { get; set; }
            public string paxtype { get; set; }
            public double api_totalprice { get; set; }
            public double api_baseprice { get; set; }
            public double api_tax { get; set; }
            public double net_totalprice { get; set; }
            public double net_baseprice { get; set; }
            public double net_tax { get; set; }
            public string api_currency { get; set; }
        }

        public class pax_fareDetailsBySegment
        {
            public string segmentid { get; set; }
            public List<segment_pax_detail> segment_pax_detail { get; set; }
        }

        public class segment_pax_detail
        {
            public string cabin { get; set; }
            public string fareBasis { get; set; }
            public string class_code { get; set; }
            public string paxtype { get; set; }
            public string paxid { get; set; }
            public string baggage { get; set; }
        }
        public class fareDetailsgroup
        {
            public int segmentid { get; set; }
            public List<groupfare> groupfare { get; set; }
        }
        public class groupfare
        {
            public string rbd { get; set; }
            public string cabin { get; set; }
            public string fareBasis { get; set; }
            public int groupindex { get; set; }
        }

        public class spirit_airline
        {
            public string code { get; set; }
            public string segment_ref { get; set; }
            public string leg_ref { get; set; }
        }

        public class OutboundInbounddata
        {
            public string totaltime { get; set; }
            public List<flightlist> flightlist { get; set; }
        }
        public class flightlist
        {
            public string api_segment_id { get; set; }
            public string seats_remaining { get; set; }
            public string Aircraft { get; set; }
            public Arrivaldeparture Arrival { get; set; }
            public Arrivaldeparture Departure { get; set; }
            public List<Checkinbaggage> CheckinBaggage { get; set; }
            public string FlightMinutes { get; set; }
            public string FlightTime { get; set; }
            public string CabinClassText { get; set; }
            public operating_airline MarketingAirline { get; set; }
            public operating_airline OperatingAirline { get; set; }
            public string connectiontime { get; set; }
            public string booking_code { get; set; }
            public int flightindex { get; set; }
            public string farePassengerTypeCode { get; set; }
            public List<technicalStop> technicalStop { get; set; }
            public string RBD { get; set; }
            public List<string> RBDLIST { get; set; }
            public string availabilityCnxType_slice_dice { get; set; }
        }

        public class Arrivaldeparture
        {
            public string city { get; set; }
            public string name { get; set; }
            public string Datetime { get; set; }
            public string apidate { get; set; }
            public string Date { get; set; }
            public string time { get; set; }
            public string Iata { get; set; }
            public string Terminal { get; set; }
        }
        public class technicalStop
        {
            public List<stopDetails> stopDetails { get; set; }
            public string stopconnectiontime { get; set; }
            public int stop_totalminutes { get; set; }
        }

        public class stopDetails
        {
            public string dateQualifier { get; set; }
            public string date { get; set; }
            public string firstTime { get; set; }
            public string dateTime { get; set; }
            public string equipementType { get; set; }
            public string locationId { get; set; }
            public string location_city { get; set; }
            public string location_name { get; set; }
        }
        public class Checkinbaggage
        {
            public string Type { get; set; }
            public string Value { get; set; }
            public string price { get; set; }
        }
        public class operating_airline
        {
            public string name { get; set; }
            public string number { get; set; }
            public string code { get; set; }
        }
    }
}
