using librets;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Rodzilla.RealEstate.Models;

namespace Rodzilla.RealEstate
{
    public static class FetchListings
    {
        [FunctionName("FetchListings")]
        public static async Task Run([QueueTrigger("incoming-property-request-job", Connection = "AzureWebJobsStorage")]SearchRequestor requestor, ILogger log,
            [Queue("incomingqueue-activeproperties", Connection = "AzureWebJobsStorage")]IAsyncCollector<MlsListing> listingAsyncCollector)
        {
            //log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            var loginUrl = Environment.GetEnvironmentVariable("RETSLoginUrl");
            var login = Environment.GetEnvironmentVariable("RETSLogin");
            var password = Environment.GetEnvironmentVariable("RETSPassword");

            using (var retsSession = new RetsSession(loginUrl))
            {
                var uri = new Uri(loginUrl);
                if (uri.Scheme == Uri.UriSchemeHttps)
                {
                    retsSession.SetModeFlags(RetsSession.MODE_NO_SSL_VERIFY);
                }

                retsSession.SetRetsVersion(RetsVersion.RETS_1_8_0);

                if (!retsSession.Login(login, password))
                {
                    throw new Exception("RETS login failed");
                }

                //var lastHalfHour = DateTime.Now.ToUniversalTime().Subtract(new TimeSpan(1, 15, 30, 0)).ToString("yyyy-MM-ddTHH:mm:ss.fff");

                //create search request
                var searchRequest = retsSession.CreateSearchRequest("Property", "Listing", $"(LastChangeTimestamp={requestor.TimeSpan}+),(PropertyType=RES,MUL,LOT,FRM,COM),(Status=A)");
                if (searchRequest != null)
                {
                    searchRequest.SetStandardNames(false);
                    searchRequest.SetFormatType(SearchRequest.FormatType.COMPACT_DECODED);

                    searchRequest.SetSelect("PhotoCount,Matrix_Unique_ID,PropertyType,ListPrice,SqFtTotal,LotSize,BedsTotal,BathsTotal,GarageSpaces,YearBuilt,MLSNumber,City,CountyOrParish,StreetNumber,StreetDirPrefix,StreetName,StreetSuffix,PostalCode,StateOrProvince,Style,PublicRemarks");
                    searchRequest.SetLimit(requestor.ResultsLimit);
                    searchRequest.SetOffset(SearchRequest.OFFSET_NONE);
                    searchRequest.SetCountType(SearchRequest.CountType.RECORD_COUNT_AND_RESULTS);
                    searchRequest.SetQueryType(SearchRequest.QueryType.DMQL2);

                    //List<MlsListing> listings;
                    using (var results = retsSession.Search(searchRequest))
                    {
                        var columns = results.GetColumns();
                        //listings = new List<MlsListing>();
                        while (results.HasNext())
                        {
                            var listingObj = new JObject();
                            foreach (var column in columns)
                            {
                                listingObj.Add(new JProperty(column, results.GetString(column)));
                            }

                            var listing = JsonConvert.DeserializeObject<MlsListing>(listingObj.ToString());
                            //add to the queue
                            await listingAsyncCollector.AddAsync(listing);
                        }
                    }
                }

            }

        }
    }
}
