using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Rodzilla.RealEstate.Models
{
    [JsonObject(MemberSerialization.OptIn, ItemNullValueHandling = NullValueHandling.Ignore)]
    public class MlsListing
    {

        [JsonProperty] public string Title => StreetAddress;

        [JsonProperty("PublicRemarks")]
        public string Content { get; set; }

        [JsonProperty("ListPrice")]
        public double SalePrice { get; set; }

        [JsonProperty("SqftTotal")]
        public int Size { get; set; }

        [JsonProperty("LotSize")]
        public string LotSize { get; set; }
        [JsonProperty("BedsTotal")]
        public int Bedrooms { get; set; }

        [JsonProperty("BathsTotal")]
        public double Bathrooms { get; set; }

        [JsonProperty]
        public int GarageSpaces { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int YearBuilt { get; set; }

        [JsonProperty("MLSNumber")]
        public string MlsId { get; set; }

        [JsonProperty("Matrix_Unique_Id")]
        public string MatrixId { get; set; }

        [JsonProperty("Style")]
        public string Type { get; set; }

        [JsonProperty]
        public int PhotoCount { get; set; }

        [JsonProperty()]
        public string PropertyType { get; set; }

        [JsonProperty()]
        public string City { get; set; }
        [JsonProperty("CountyOrParish")]
        public string County { get; set; }
        [JsonProperty]
        public string PostalCode { get; set; }

        [JsonProperty]
        public string FullAddress => $"{StreetAddress}, {City}, {State} {PostalCode}";

        [JsonProperty]
        public string StreetAddress => $"{StreetNumber.Trim()}{((StreetDirPrefix.Trim() != "") ? $" {StreetDirPrefix.Trim()} " : " ")}{StreetName.Trim()}{((StreetSuffix.Trim() != "") ? $" {StreetSuffix.Trim()}" : "")}";

        [JsonProperty]
        private string StreetNumber { get; set; }
        [JsonProperty]
        private string StreetDirPrefix { get; set; }
        [JsonProperty]
        private string StreetName { get; set; }
        [JsonProperty]
        private string StreetSuffix { get; set; }
        [JsonProperty("StateOrProvince")]
        public string State { get; set; }

        public double Longitude { get; set; }
        public double Latitude { get; set; }

        public async Task LoadGeocode()
        {
            var url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(FullAddress)},+CA&key={Environment.GetEnvironmentVariable("GoogleApiKey")}";
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/json; charset=utf-8";

            var response = await request.GetResponseAsync() as HttpWebResponse;
            using (var responseStream = response?.GetResponseStream())
            {
                if (responseStream == null) return;
                var reader = new StreamReader(responseStream, Encoding.UTF8);
                var jsonObj = JObject.Parse(reader.ReadToEnd());
                Longitude = (double) jsonObj["results"][0]["geometry"]["location"]["lng"];
                Latitude = (double) jsonObj["results"][0]["geometry"]["location"]["lat"];
            }

        }
    }
}
