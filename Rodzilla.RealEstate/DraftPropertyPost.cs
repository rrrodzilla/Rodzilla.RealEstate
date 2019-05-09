using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using Rodzilla.RealEstate.Models;
using System;
using System.Globalization;
using System.Threading.Tasks;
using WordPressPCL;
using WordPressPCL.Models;

namespace Rodzilla.RealEstate
{
    public static class DraftPropertyPost
    {
        [FunctionName("DraftPropertyPost")]
        public static async Task Run([QueueTrigger("incomingqueue-activeproperties", Connection = "AzureWebJobsStorage")]MlsListing incomingPropertyListing, ILogger log,
            [Queue("incoming-property-meta", Connection = "AzureWebJobsStorage")]IAsyncCollector<PostMetaValue> metaAsyncCollector)
        {
            log.LogInformation($"\n\n\nIncoming Property for Draft Post: {incomingPropertyListing}");

            var client = new WordPressClient(Environment.GetEnvironmentVariable("WordpressUrl"))
            {
                AuthMethod = AuthMethod.JWT
            };


            log.LogInformation($"Getting JWToken for Draft Post: {incomingPropertyListing}");
            await client.RequestJWToken(Environment.GetEnvironmentVariable("WordpressUsername"), Environment.GetEnvironmentVariable("WordpressPassword"));
            var isValidToken = await client.IsValidJWToken();
            // Posts

            //JObject draftPost = null;
            log.LogInformation("Initialized draftPost");
            JObject post = null;
            if (isValidToken)
            {
                try
                {
                    var draftPost = new PropertyPost(incomingPropertyListing) { Status = Status.Publish };

                    //trying to publish it
                    post = await client.CustomRequest.Create<PropertyPost, JObject>("wp/v2/properties", draftPost);
                    log.LogInformation($"Published post {post.Property("id").Value} for MLS ID {incomingPropertyListing.MlsId}");

                }
                catch (Exception e)
                {
                    log.LogInformation($"An ERROR WHILE DRAFTING POST");
                    log.LogInformation(e.ToString());
                }
            }

            if (post != null)
            {
                var postId = post.Property("id").Value.ToString();
                await incomingPropertyListing.LoadGeocode();

                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_id", MetaValue = incomingPropertyListing.MlsId });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_price", MetaValue = incomingPropertyListing.SalePrice.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_size", MetaValue = incomingPropertyListing.Size.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_size_prefix", MetaValue = "Sq Ft" });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_bedrooms", MetaValue = incomingPropertyListing.Bedrooms.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_bathrooms", MetaValue = incomingPropertyListing.Bathrooms.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_garage", MetaValue = incomingPropertyListing.GarageSpaces.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_year", MetaValue = incomingPropertyListing.YearBuilt.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_map_address", MetaValue = incomingPropertyListing.FullAddress.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_address", MetaValue = incomingPropertyListing.StreetAddress.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_zip", MetaValue = incomingPropertyListing.PostalCode.ToString(CultureInfo.CurrentCulture) });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_location", MetaValue = $"{incomingPropertyListing.Latitude},{incomingPropertyListing.Longitude}" });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                { PostId = postId, Listing = incomingPropertyListing, MetaKey = "houzez_geolocation_lat", MetaValue = $"{incomingPropertyListing.Latitude}" });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                    { PostId = postId, Listing = incomingPropertyListing, MetaKey = "houzez_geolocation_long", MetaValue = $"{incomingPropertyListing.Longitude}" });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                    { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_map_street_view", MetaValue = "show" });
                await metaAsyncCollector.AddAsync(new PostMetaValue()
                    { PostId = postId, Listing = incomingPropertyListing, MetaKey = "fave_property_map", MetaValue = "1" });

                //publish post
                using (var conn = new MySqlConnection(Environment.GetEnvironmentVariable("MySqlStorage")))
                {
                    try
                    {
                        conn.Open();
                        //here we want to make sure the term doesn't already exist in the db
                        var sql = "UPDATE 16c_posts SET post_status='publish' WHERE ID = @postId";
                        var cmd = new MySqlCommand(sql, conn);
                        var paramPostId = cmd.CreateParameter();
                        paramPostId.ParameterName = "@postId";
                        paramPostId.Value = postId;
                        cmd.Parameters.Add(paramPostId);
                        await cmd.ExecuteNonQueryAsync();

                        log.LogInformation($"PUBLISHED POST: {postId}");
                    }
                    catch (Exception ex)
                    {
                        log.LogInformation($"ERROR PUBLISHING POST ID:{postId} --- REVIEW Exception.");
                        log.LogInformation(ex.ToString());
                    }

                    conn.Close();

                }
            }
        }

    }
}
