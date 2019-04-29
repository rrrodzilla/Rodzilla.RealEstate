using librets;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Rodzilla.RealEstate
{
    public static class FetchListings
    {
        [FunctionName("FetchListings")]
        public static async Task Run([TimerTrigger("0 */4 * * * *")]TimerInfo myTimer, ILogger log)
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

                var lastHalfHour = DateTime.Now.ToUniversalTime().Subtract(new TimeSpan(30, 15, 30, 0)).ToString("yyyy-MM-ddTHH:mm:ss.fff");
                //log.LogInformation(lastHalfHour);
                var searchRequest = retsSession.CreateSearchRequest("Property", "Listing", $"(LastChangeTimestamp={lastHalfHour}+),(PropertyType=RES,MUL,LOT,FRM,COM),(Status=A)");
                if (searchRequest != null)
                {
                    searchRequest.SetStandardNames(false);
                    searchRequest.SetFormatType(SearchRequest.FormatType.COMPACT_DECODED);

                    searchRequest.SetSelect("PhotoCount,Matrix_Unique_ID,PropertyType,ListPrice,SqFtTotal,LotSize,BedsTotal,BathsTotal,GarageSpaces,YearBuilt,MLSNumber,City,CountyOrParish,StreetNumber,StreetDirPrefix,StreetName,StreetSuffix,PostalCode,StateOrProvince,Style,PublicRemarks");
                    searchRequest.SetLimit(50);
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
                            await QueueProperty(listing);
                            //listings.Add(listing);
                        }
                    }
                    //now we want to push each of these properties into a queue

                    //log.LogInformation(JsonConvert.SerializeObject(listings, Formatting.Indented));
                }

            }

        }

        private static async Task QueueProperty(MlsListing propertyListing)
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            // Create the queue client.
            var queueClient = storageAccount.CreateCloudQueueClient();

            // Retrieve a reference to a queue.
            var queue = queueClient.GetQueueReference(Environment.GetEnvironmentVariable("IncomingPropertyQueue"));

            // Create the queue if it doesn't already exist.
            await queue.CreateIfNotExistsAsync();

            // Create a message and add it to the queue.
            var message = new CloudQueueMessage(JsonConvert.SerializeObject(propertyListing));
            await queue.AddMessageAsync(message);
        }
    }
}
