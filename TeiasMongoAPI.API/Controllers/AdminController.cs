using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.API.Controllers
{
    /// <summary>
    /// Administrative operations controller
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMongoDatabase _database;

        public AdminController(
            IUnitOfWork unitOfWork,
            IMongoDatabase database,
            ILogger<AdminController> logger)
            : base(logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Migrates program creator data from full names to ObjectIds
        /// </summary>
        /// <remarks>
        /// This is a one-time migration endpoint that:
        /// 1. Fetches all programs and users from the database
        /// 2. Creates a lookup dictionary mapping user full names to their ObjectIds
        /// 3. Updates each program's CreatorId field based on the Creator field (which contains the full name)
        /// 4. Logs success and failure cases
        /// 5. Returns a summary of the migration results
        /// 
        /// Programs that already have a valid CreatorId will be skipped.
        /// Programs with Creator names that don't match any user will be logged as warnings but not updated.
        /// </remarks>
        /// <returns>Migration summary with counts of scanned, migrated, and failed records</returns>
        [HttpPost("migrate-program-creators")]
        public async Task<ActionResult<ApiResponse<object>>> MigrateProgramCreators(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting program creator migration process");

                // Get MongoDB collections directly for BsonDocument operations
                var programsCollection = _database.GetCollection<BsonDocument>("programs");
                var usersCollection = _database.GetCollection<BsonDocument>("users");

                // Fetch all programs using BsonDocument to access old fields
                var programDocuments = await programsCollection.Find(new BsonDocument()).ToListAsync(cancellationToken);
                _logger.LogInformation("Found {ProgramCount} programs to process", programDocuments.Count);

                // Fetch all users and create lookup dictionary (FullName -> _id.ToString())
                var userDocuments = await usersCollection.Find(new BsonDocument()).ToListAsync(cancellationToken);
                var userLookup = new Dictionary<string, string>();

                foreach (var userDoc in userDocuments)
                {
                    if (userDoc.Contains("firstName") && userDoc.Contains("lastName") && userDoc.Contains("_id"))
                    {
                        var fullName = userDoc["firstName"].AsString + " " + userDoc["lastName"].AsString;
                        var userId = userDoc["_id"].AsObjectId.ToString();
                        
                        if (!string.IsNullOrEmpty(fullName) && !userLookup.ContainsKey(fullName))
                        {
                            userLookup[fullName] = userId;
                        }
                        else if (userLookup.ContainsKey(fullName))
                        {
                            _logger.LogWarning("Duplicate user FullName found: '{FullName}'. Only the first user will be used for migration.", fullName);
                        }
                    }
                }

                _logger.LogInformation("Created user lookup dictionary with {UserCount} entries", userLookup.Count);

                // Migration counters
                int scanned = 0;
                int migrated = 0;
                int failed = 0;

                // Process each program document
                foreach (var programDoc in programDocuments)
                {
                    scanned++;
                    
                    try
                    {
                        var programId = programDoc["_id"].AsObjectId.ToString();

                        // Check if document already has a valid CreatorId
                        if (programDoc.Contains("CreatorId") && !string.IsNullOrEmpty(programDoc["CreatorId"].AsString))
                        {
                            _logger.LogDebug("Program ID {ProgramId} already has CreatorId, skipping", programId);
                            continue;
                        }

                        // Get the old Creator field (contains full name)
                        if (!programDoc.Contains("Creator"))
                        {
                            _logger.LogWarning("Migration SKIPPED for Program ID {ProgramId}. Reason: No 'creator' field found.", programId);
                            failed++;
                            continue;
                        }

                        var creatorName = programDoc["Creator"].AsString;
                        
                        if (string.IsNullOrEmpty(creatorName))
                        {
                            _logger.LogWarning("Migration SKIPPED for Program ID {ProgramId}. Reason: Creator field is empty.", programId);
                            failed++;
                            continue;
                        }

                        // Look up user ID by full name
                        if (userLookup.ContainsKey(creatorName))
                        {
                            var creatorId = userLookup[creatorName];

                            // Update the document with CreatorId
                            var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(programId));
                            var update = Builders<BsonDocument>.Update.Set("CreatorId", creatorId);
                            
                            var resultCollection = await programsCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

                            if (resultCollection.ModifiedCount > 0)
                            {
                                _logger.LogInformation("Successfully migrated Program ID {ProgramId}, setting CreatorId for '{CreatorName}'", programId, creatorName);
                                migrated++;
                            }
                            else
                            {
                                _logger.LogWarning("Migration FAILED for Program ID {ProgramId}. Database update did not modify any document.", programId);
                                failed++;
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Migration SKIPPED for Program ID {ProgramId}. Reason: Could not find a user with name '{CreatorName}'.", programId, creatorName);
                            failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        var programId = programDoc.Contains("_id") ? programDoc["_id"].AsObjectId.ToString() : "Unknown";
                        _logger.LogError(ex, "Migration ERROR for Program ID {ProgramId}. Exception: {Exception}", programId, ex.Message);
                        failed++;
                    }
                }

                var result = new 
                { 
                    Scanned = scanned, 
                    Migrated = migrated, 
                    Failed = failed,
                    TotalUsers = userLookup.Count
                };

                _logger.LogInformation("Migration completed. Scanned: {Scanned}, Migrated: {Migrated}, Failed: {Failed}", 
                    result.Scanned, result.Migrated, result.Failed);

                return Success((object)result, "Program creator migration completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error during program creator migration");
                return HandleException<object>(ex, "migrate program creators");
            }
        }
    }
}