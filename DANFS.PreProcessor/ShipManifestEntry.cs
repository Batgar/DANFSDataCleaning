using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DANFS.PreProcessor
{
    class ShipManifestEntry
    {
        [JsonProperty(PropertyName ="title")]
        public string Title { get; set; }


        [JsonProperty(PropertyName ="id")]
        public string ID { get; set; }
    }
}
