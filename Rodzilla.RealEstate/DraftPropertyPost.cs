using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using WordPressPCL;
using WordPressPCL.Models;

namespace Rodzilla.RealEstate
{
    public static class DraftPropertyPost
    {
        [FunctionName("DraftPropertyPost")]
        public static async Task Run([QueueTrigger("incomingqueue-activeproperties", Connection = "AzureWebJobsStorage")]MlsListing incomingPropertyListing, ILogger log)
        {
            log.LogInformation($"Incoming Property for Draft Post: {incomingPropertyListing}");

            var client = new WordPressClient(Environment.GetEnvironmentVariable("WordpressUrl"))
            {
                AuthMethod = AuthMethod.JWT
            };
            //JsonSerializerSettings = new JsonSerializerSettings()
            //{
            //    DateFormatHandling = DateFormatHandling.IsoDateFormat,
            //    DateFormatString = "d MMMM YYYY",
            //    CheckAdditionalContent = true
            //}




            await client.RequestJWToken(Environment.GetEnvironmentVariable("WordpressUsername"), Environment.GetEnvironmentVariable("WordpressPassword"));
            var isValidToken = await client.IsValidJWToken();
            // Posts

            JObject draftPost = null;
            log.LogInformation("Initialized draftPost");
            if (isValidToken)
            {
                try
                {
                    var post = new PropertyPost(incomingPropertyListing);

                    draftPost = await client.CustomRequest.Create<PropertyPost, JObject>("wp/v2/properties", post);
                    log.LogInformation($"Drafted post {draftPost.Property("Id")} for MLS ID {incomingPropertyListing.MlsId}");
                    log.LogInformation("Created Draft Post");
                }
                catch (Exception e)
                {
                    log.LogInformation($"An ERROR WHILE DRAFTING POST");
                    log.LogInformation(e.ToString());
                }
            }

            if (draftPost != null)
            {
                var postId = draftPost.Property("id").Value.ToString();
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_id", incomingPropertyListing.MlsId, log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_price", incomingPropertyListing.SalePrice.ToString(CultureInfo.CurrentCulture), log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_size", incomingPropertyListing.Size.ToString(CultureInfo.CurrentCulture), log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_size_prefix", "Sq Ft", log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_bedrooms", incomingPropertyListing.Bedrooms.ToString(CultureInfo.CurrentCulture), log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_bathrooms", incomingPropertyListing.Bathrooms.ToString(CultureInfo.CurrentCulture), log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_garage", incomingPropertyListing.GarageSpaces.ToString(CultureInfo.CurrentCulture), log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_year", incomingPropertyListing.YearBuilt.ToString(CultureInfo.CurrentCulture), log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_map_address", incomingPropertyListing.FullAddress.ToString(CultureInfo.CurrentCulture), log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_address", incomingPropertyListing.StreetAddress.ToString(CultureInfo.CurrentCulture), log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_zip", incomingPropertyListing.PostalCode.ToString(CultureInfo.CurrentCulture), log);
                await incomingPropertyListing.LoadGeocode();
                await InsertPropertyTerm(postId, incomingPropertyListing, "fave_property_location", $"{incomingPropertyListing.Latitude},{incomingPropertyListing.Longitude}", log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "houzez_geolocation_lat", $"{incomingPropertyListing.Latitude}", log);
                await InsertPropertyTerm(postId, incomingPropertyListing, "houzez_geolocation_long", $"{incomingPropertyListing.Longitude}", log);

            }
        }

        private static async Task InsertPropertyTerm(string postId, MlsListing listing, string metaKey, string value, ILogger log)
        {
//now we should add all the terms directly into the database for this property
            using (var conn = new MySqlConnection(Environment.GetEnvironmentVariable("MySqlStorage")))
            {
                try
                {
                    conn.Open();
                    //here we want to make sure the term doesn't already exist in the db
                    var sql = $"SELECT post_id, meta_key, meta_value FROM 16c_postmeta WHERE post_id=@postId and meta_key='@metaKey' and meta_value='@metaValue'";
                    var cmd = new MySqlCommand(sql, conn);
                    var paramPostId = cmd.CreateParameter();
                    paramPostId.ParameterName = "@postId";
                    paramPostId.Value = postId;
                    cmd.Parameters.Add(paramPostId);

                    var paramMetaKey = cmd.CreateParameter();
                    paramMetaKey.ParameterName = "@metaKey";
                    paramMetaKey.Value = metaKey;
                    cmd.Parameters.Add(paramMetaKey);

                    var paramMetaValue = cmd.CreateParameter();
                    paramMetaValue.ParameterName = "@metaValue";
                    paramMetaValue.Value = value;
                    cmd.Parameters.Add(paramMetaValue);

                    var rdr = await cmd.ExecuteReaderAsync();
                    if (rdr.HasRows)
                    {
                        log.LogInformation($"Post {postId} for MLS ID:{listing.MlsId} exists - skipping insert.");
                        rdr.Close();
                    }

                    rdr.Close();

                    log.LogInformation($"INSERTING {metaKey} for MLS ID:{listing.MlsId}");

                    //insert into terms table
                    //note new id
                    sql = $"INSERT INTO 16c_postmeta (post_id, meta_key, meta_value) VALUES (@postId,@metaKey,@metaValue)";
                    cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.Add(paramPostId);
                    cmd.Parameters.Add(paramMetaKey);
                    cmd.Parameters.Add(paramMetaValue);
                    await cmd.ExecuteNonQueryAsync();

                }
                catch (Exception ex)
                {
                    log.LogInformation($"ERROR INSERTING MLS ID:{listing.MlsId} REVIEW Exception.");
                    log.LogInformation(ex.ToString());
                }

                conn.Close();
            }
        }
    }
}
