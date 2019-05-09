using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Rodzilla.RealEstate.Models;

namespace Rodzilla.RealEstate
{
    public static class InsertPropertyMetaValue
    {
        [FunctionName("InsertPropertyMetaValue")]
        public static async Task Run([QueueTrigger("incoming-property-meta", Connection = "AzureWebJobsStorage")]PostMetaValue postMetaValue, ILogger log)
        {
            using (var conn = new MySqlConnection(Environment.GetEnvironmentVariable("MySqlStorage")))
            {
                try
                {
                    log.LogInformation($"\n\n\nIncoming MetaValue for post: {postMetaValue.PostId}");
                    conn.Open();
                    //here we want to make sure the term doesn't already exist in the db
                    var sql = "SELECT post_id, meta_key, meta_value FROM 16c_postmeta WHERE post_id=@postId and meta_key='@metaKey' and meta_value='@metaValue'";
                    var cmd = new MySqlCommand(sql, conn);
                    var paramPostId = cmd.CreateParameter();
                    paramPostId.ParameterName = "@postId";
                    paramPostId.Value = postMetaValue.PostId;
                    cmd.Parameters.Add(paramPostId);

                    var paramMetaKey = cmd.CreateParameter();
                    paramMetaKey.ParameterName = "@metaKey";
                    paramMetaKey.Value = postMetaValue.MetaKey;
                    cmd.Parameters.Add(paramMetaKey);

                    var paramMetaValue = cmd.CreateParameter();
                    paramMetaValue.ParameterName = "@metaValue";
                    paramMetaValue.Value = postMetaValue.MetaValue;
                    cmd.Parameters.Add(paramMetaValue);

                    var rdr = await cmd.ExecuteReaderAsync();
                    if (rdr.HasRows)
                    {
                        log.LogInformation($"Post {postMetaValue.PostId} for MLS ID:{postMetaValue.Listing.MlsId} exists - skipping insert.");
                        rdr.Close();

                    }

                    rdr.Close();

                    log.LogInformation($"INSERTING {postMetaValue.MetaKey} for MLS ID:{postMetaValue.Listing.MlsId} with Value: {postMetaValue.MetaValue}");

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
                    log.LogInformation($"ERROR INSERTING MLS ID:{postMetaValue.Listing.MlsId} REVIEW Exception.");
                    log.LogInformation(ex.ToString());
                }

                conn.Close();
                log.LogInformation($"MetaValue POSTED for post: {postMetaValue.PostId}\n\n\n");
            }
        }
    }
}
