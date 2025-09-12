using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Data.Context;

namespace TeiasMongoAPI.API.Controllers
{
    /// <summary>
    /// Temporary controller for database migrations - should be removed after migrations complete
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MigrationController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly MongoDbContext _context;
        private readonly ILogger<MigrationController> _logger;

        public MigrationController(IUnitOfWork unitOfWork, MongoDbContext context, ILogger<MigrationController> logger)
        {
            _unitOfWork = unitOfWork;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Migration to fix "system" reviewer entries in versions collection
        /// This endpoint should be called once and then removed from the codebase
        /// </summary>
        [HttpPost("fix-system-reviewer-entries")]
        public async Task<IActionResult> FixSystemReviewerEntries()
        {
            try
            {
                _logger.LogInformation("Starting migration to fix system reviewer entries");

                // Get direct access to the MongoDB collection
                var database = _context.Database;
                var collection = database.GetCollection<BsonDocument>("versions");

                // Find all documents where Reviewer field equals "system"
                var filter = Builders<BsonDocument>.Filter.Eq("Reviewer", "system");
                var documentsToUpdate = await collection.Find(filter).ToListAsync();

                _logger.LogInformation("Found {DocumentCount} documents with 'system' reviewer entries", documentsToUpdate.Count);

                if (documentsToUpdate.Count == 0)
                {
                    return Ok(new { message = "No documents found with 'system' reviewer entries", updated = 0 });
                }

                // Option 1: Set Reviewer to null for system entries
                // This is the safest option as it removes the invalid data without making assumptions
                var update = Builders<BsonDocument>.Update.Unset("Reviewer");
                
                var result = await collection.UpdateManyAsync(filter, update);

                _logger.LogInformation("Migration completed. Updated {ModifiedCount} documents", result.ModifiedCount);

                return Ok(new 
                { 
                    message = "Successfully fixed system reviewer entries",
                    documentsFound = documentsToUpdate.Count,
                    documentsUpdated = result.ModifiedCount,
                    action = "Set Reviewer field to null for system entries"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during system reviewer entries migration");
                return StatusCode(500, new { message = "Migration failed", error = ex.Message });
            }
        }

        /// <summary>
        /// Alternative migration that creates a system user and assigns its ObjectId
        /// Use this if business logic requires a proper user reference for system reviews
        /// </summary>
        [HttpPost("create-system-user-and-fix-entries")]
        public async Task<IActionResult> CreateSystemUserAndFixEntries()
        {
            try
            {
                _logger.LogInformation("Starting migration to create system user and fix entries");

                // TODO: Implement this if business requirements need a proper system user
                // 1. Check if system user already exists
                // 2. If not, create a system user with proper ObjectId
                // 3. Update all "system" reviewer entries to use the system user's ObjectId
                
                return Ok(new { message = "This migration is not implemented yet - requires business decision" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during system user creation migration");
                return StatusCode(500, new { message = "Migration failed", error = ex.Message });
            }
        }

        /// <summary>
        /// Query to check how many documents have "system" reviewer entries
        /// Use this to verify the problem before running migrations
        /// </summary>
        [HttpGet("check-system-reviewer-entries")]
        public async Task<IActionResult> CheckSystemReviewerEntries()
        {
            try
            {
                var database = _context.Database;
                var collection = database.GetCollection<BsonDocument>("versions");

                // Count documents where Reviewer field equals "system"
                var filter = Builders<BsonDocument>.Filter.Eq("Reviewer", "system");
                var count = await collection.CountDocumentsAsync(filter);

                // Get a few sample documents for analysis
                var samples = await collection.Find(filter).Limit(5).ToListAsync();

                return Ok(new 
                { 
                    systemReviewerCount = count,
                    sampleDocuments = samples.Select(doc => new 
                    {
                        id = doc.GetValue("_id"),
                        reviewer = doc.Contains("Reviewer") ? doc.GetValue("Reviewer") : null,
                        createdAt = doc.Contains("CreatedAt") ? doc.GetValue("CreatedAt") : null,
                        programId = doc.Contains("ProgramId") ? doc.GetValue("ProgramId") : null
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking system reviewer entries");
                return StatusCode(500, new { message = "Check failed", error = ex.Message });
            }
        }
    }
}