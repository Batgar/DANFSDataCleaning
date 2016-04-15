using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DANFS.PreProcessor
{
    class GoogleGeocoder
    {
        public async Task<GeocodeResultMain> DoGecode(string address)
        {
            var client = new HttpClient();

            var uriString = string.Format("http://maps.googleapis.com/maps/api/geocode/json?address={0}&sensor=false", Uri.EscapeDataString(address));

            Uri uri = new Uri(uriString);

            var jsonRawString = await client.GetStringAsync(uri);

            return Newtonsoft.Json.JsonConvert.DeserializeObject<GeocodeResultMain>(jsonRawString);
        }
    }

    
    class GeocodeResultMain
    {
        [JsonProperty(PropertyName = "results")]
        public List<GeocodeResult> Results { get; set; }

        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }
    }


    class GeocodeResult
    {
        [JsonProperty(PropertyName = "address_components")]
        public List<GeocodeAddressComponent> AddressComponents { get; set; }
        
        [JsonProperty(PropertyName = "formatted_address")]
        public string FormattedAddress { get; set; }

        [JsonProperty(PropertyName = "geometry")]
        public GeocodeGeometry Geometry { get; set; }

        [JsonProperty(PropertyName = "place_id")]
        public string PlaceID { get; set; }

        [JsonProperty(PropertyName = "types")]
        public List<string> Types { get; set; }
    }

    class GeocodeGeometry
    {
        [JsonProperty(PropertyName ="bounds")]
        public Dictionary<string, GeocodeLatLong> Bounds { get; set; }

        [JsonProperty(PropertyName = "location_type")]
        public string LocationType { get; set; }

        [JsonProperty(PropertyName = "viewport")]
        public Dictionary<string, GeocodeLatLong> Viewport { get; set; }
    }

    

    class GeocodeLatLong
    {
        [JsonProperty(PropertyName ="lat")]
        public double Lat { get; set; }

        [JsonProperty(PropertyName = "lng")]
        public double Long { get; set; }
    }

    class GeocodeAddressComponent
    {
        [JsonProperty(PropertyName ="long_name")]
        public string LongName { get; set; }

        [JsonProperty(PropertyName = "short_name")]
        public string ShortName { get; set; }

        [JsonProperty(PropertyName = "types")]
        public List<string> Types { get; set; }
    }
}
