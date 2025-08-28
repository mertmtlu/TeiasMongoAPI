using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Data.Configuration;

namespace TeiasMongoAPI.Data.Services
{
    public interface IMongoDbIndexInitializer
    {
        Task InitializeIndexesAsync(CancellationToken cancellationToken = default);
    }

    public class MongoDbIndexInitializer : IMongoDbIndexInitializer
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoDbIndexInitializer> _logger;

        public MongoDbIndexInitializer(IMongoClient mongoClient, IOptions<MongoDbSettings> settings, ILogger<MongoDbIndexInitializer> logger)
        {
            _database = mongoClient.GetDatabase(settings.Value.DatabaseName);
            _logger = logger;
        }

        public async Task InitializeIndexesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting MongoDB index initialization...");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // MODIFICATION: Create indexes for Programs collection
                await CreateProgramsIndexesAsync(cancellationToken);
                
                // MODIFICATION: Create indexes for Workflows collection
                await CreateWorkflowsIndexesAsync(cancellationToken);
                
                // MODIFICATION: Create indexes for Users collection
                await CreateUsersIndexesAsync(cancellationToken);
                
                // MODIFICATION: Create indexes for Versions collection
                await CreateVersionsIndexesAsync(cancellationToken);
                
                // MODIFICATION: Create indexes for UiComponents collection
                await CreateUiComponentsIndexesAsync(cancellationToken);

                stopwatch.Stop();
                _logger.LogInformation("MongoDB index initialization completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MongoDB indexes");
                throw;
            }
        }

        private async Task CreateProgramsIndexesAsync(CancellationToken cancellationToken)
        {
            var collection = _database.GetCollection<BsonDocument>("programs");
            
            var indexes = new List<CreateIndexModel<BsonDocument>>
            {
                // MODIFICATION: Index for user permissions lookup - critical for GetUserAccessiblePrograms
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("permissions.users.userId"),
                    new CreateIndexOptions { Name = "idx_permissions_users_userId", Background = true }
                ),
                
                // MODIFICATION: Index for status filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("status"),
                    new CreateIndexOptions { Name = "idx_status", Background = true }
                ),
                
                // MODIFICATION: Index for created date sorting
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Descending("createdAt"),
                    new CreateIndexOptions { Name = "idx_createdAt", Background = true }
                ),
                
                // MODIFICATION: Index for creator filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("creatorId"),
                    new CreateIndexOptions { Name = "idx_creatorId", Background = true }
                ),
                
                // MODIFICATION: Index for name uniqueness check
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("name"),
                    new CreateIndexOptions { Name = "idx_name_unique", Unique = true, Background = true }
                ),
                
                // MODIFICATION: Compound index for type and language filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("type").Ascending("language"),
                    new CreateIndexOptions { Name = "idx_type_language", Background = true }
                ),
                
                // MODIFICATION: Index for current version lookup
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("currentVersion"),
                    new CreateIndexOptions { Name = "idx_currentVersion", Background = true }
                )
            };

            await CreateIndexesSafelyAsync(collection, indexes, "programs", cancellationToken);
        }

        private async Task CreateWorkflowsIndexesAsync(CancellationToken cancellationToken)
        {
            var collection = _database.GetCollection<BsonDocument>("workflows");
            
            var indexes = new List<CreateIndexModel<BsonDocument>>
            {
                // MODIFICATION: Index for status filtering - critical for GetAllAsync performance
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("status"),
                    new CreateIndexOptions { Name = "idx_status", Background = true }
                ),
                
                // MODIFICATION: Index for created date sorting
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Descending("createdAt"),
                    new CreateIndexOptions { Name = "idx_createdAt", Background = true }
                ),
                
                // MODIFICATION: Index for creator filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("creator"),
                    new CreateIndexOptions { Name = "idx_creator", Background = true }
                ),
                
                // MODIFICATION: Index for name search
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Text("name").Text("description"),
                    new CreateIndexOptions { Name = "idx_text_search", Background = true }
                ),
                
                // MODIFICATION: Index for template filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("isTemplate"),
                    new CreateIndexOptions { Name = "idx_isTemplate", Background = true }
                ),
                
                // MODIFICATION: Index for tag filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("tags"),
                    new CreateIndexOptions { Name = "idx_tags", Background = true }
                ),
                
                // MODIFICATION: Index for public workflows
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("permissions.isPublic"),
                    new CreateIndexOptions { Name = "idx_permissions_isPublic", Background = true }
                )
            };

            await CreateIndexesSafelyAsync(collection, indexes, "workflows", cancellationToken);
        }

        private async Task CreateUsersIndexesAsync(CancellationToken cancellationToken)
        {
            var collection = _database.GetCollection<BsonDocument>("users");
            
            var indexes = new List<CreateIndexModel<BsonDocument>>
            {
                // MODIFICATION: Index for username lookup
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("username"),
                    new CreateIndexOptions { Name = "idx_username_unique", Unique = true, Background = true }
                ),
                
                // MODIFICATION: Index for email lookup
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("email"),
                    new CreateIndexOptions { Name = "idx_email_unique", Unique = true, Background = true }
                )
            };

            await CreateIndexesSafelyAsync(collection, indexes, "users", cancellationToken);
        }

        private async Task CreateVersionsIndexesAsync(CancellationToken cancellationToken)
        {
            var collection = _database.GetCollection<BsonDocument>("versions");
            
            var indexes = new List<CreateIndexModel<BsonDocument>>
            {
                // MODIFICATION: Index for program ID lookup - critical for batch queries
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId"),
                    new CreateIndexOptions { Name = "idx_programId", Background = true }
                ),
                
                // MODIFICATION: Compound index for program and version number
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId").Descending("versionNumber"),
                    new CreateIndexOptions { Name = "idx_programId_versionNumber", Background = true }
                ),
                
                // MODIFICATION: Index for status filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("status"),
                    new CreateIndexOptions { Name = "idx_status", Background = true }
                )
            };

            await CreateIndexesSafelyAsync(collection, indexes, "versions", cancellationToken);
        }

        private async Task CreateUiComponentsIndexesAsync(CancellationToken cancellationToken)
        {
            var collection = _database.GetCollection<BsonDocument>("uiComponents");
            
            var indexes = new List<CreateIndexModel<BsonDocument>>
            {
                // MODIFICATION: Index for program ID lookup - critical for component counts
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId"),
                    new CreateIndexOptions { Name = "idx_programId", Background = true }
                ),
                
                // MODIFICATION: Compound index for program and version
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId").Ascending("programVersionId"),
                    new CreateIndexOptions { Name = "idx_programId_versionId", Background = true }
                ),
                
                // MODIFICATION: Index for status filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("status"),
                    new CreateIndexOptions { Name = "idx_status", Background = true }
                )
            };

            await CreateIndexesSafelyAsync(collection, indexes, "uiComponents", cancellationToken);
        }

        private async Task CreateIndexesSafelyAsync(
            IMongoCollection<BsonDocument> collection, 
            List<CreateIndexModel<BsonDocument>> indexes, 
            string collectionName, 
            CancellationToken cancellationToken)
        {
            try
            {
                // MODIFICATION: Create indexes only if they don't exist (idempotent)
                var existingIndexes = await (await collection.Indexes.ListAsync(cancellationToken)).ToListAsync(cancellationToken);
                var existingIndexNames = existingIndexes.Select(i => i["name"].AsString).ToHashSet();

                var indexesToCreate = indexes.Where(idx => 
                    idx.Options?.Name != null && !existingIndexNames.Contains(idx.Options.Name)).ToList();

                if (indexesToCreate.Any())
                {
                    _logger.LogInformation("Creating {Count} indexes for collection '{Collection}'", indexesToCreate.Count, collectionName);
                    await collection.Indexes.CreateManyAsync(indexesToCreate, cancellationToken);
                    _logger.LogInformation("Successfully created indexes for collection '{Collection}': {IndexNames}", 
                        collectionName, string.Join(", ", indexesToCreate.Select(i => i.Options?.Name)));
                }
                else
                {
                    _logger.LogInformation("All required indexes already exist for collection '{Collection}'", collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create indexes for collection '{Collection}'", collectionName);
                // MODIFICATION: Don't throw here, continue with other collections
            }
        }
    }

    // MODIFICATION: Extension method for easy registration
    public static class MongoDbIndexInitializerExtensions
    {
        public static IServiceCollection AddMongoDbIndexInitializer(this IServiceCollection services)
        {
            services.AddScoped<IMongoDbIndexInitializer, MongoDbIndexInitializer>();
            return services;
        }

        public static async Task<IServiceProvider> InitializeMongoDbIndexesAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var indexInitializer = scope.ServiceProvider.GetRequiredService<IMongoDbIndexInitializer>();
            await indexInitializer.InitializeIndexesAsync();
            return serviceProvider;
        }
    }
}