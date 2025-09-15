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
                // Create indexes for Programs collection
                await CreateProgramsIndexesAsync(cancellationToken);

                // Create indexes for Workflows collection
                await CreateWorkflowsIndexesAsync(cancellationToken);

                // Create indexes for Users collection
                await CreateUsersIndexesAsync(cancellationToken);

                // Create indexes for Versions collection
                await CreateVersionsIndexesAsync(cancellationToken);

                // Create indexes for UiComponents collection
                await CreateUiComponentsIndexesAsync(cancellationToken);

                // *** NEWLY ADDED ***
                // Create indexes for Executions collection to fix the performance bottleneck
                await CreateExecutionsIndexesAsync(cancellationToken);

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
                // Index for user permissions lookup - critical for GetUserAccessiblePrograms
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("Permissions.Users.UserId"),
                    new CreateIndexOptions { Name = "idx_permissions_users_userId", Background = true }
                ),
                
                // Index for status filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("Status"),
                    new CreateIndexOptions { Name = "idx_status", Background = true }
                ),
                
                // Index for created date sorting
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Descending("CreatedAt"),
                    new CreateIndexOptions { Name = "idx_createdAt", Background = true }
                ),
                
                // Index for creator filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("CreatorId"),
                    new CreateIndexOptions { Name = "idx_creatorId", Background = true }
                ),
                
                // Index for name uniqueness check
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("Name"),
                    new CreateIndexOptions { Name = "idx_name_unique", Unique = true, Background = true }
                ),
                
                // Compound index for type and language filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("Type").Ascending("Language"),
                    new CreateIndexOptions { Name = "idx_type_language", Background = true }
                ),
                
                // Index for current version lookup
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("CurrentVersion"),
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
                // Index for status filtering - critical for GetAllAsync performance
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("status"),
                    new CreateIndexOptions { Name = "idx_status", Background = true }
                ),
                
                // Index for created date sorting
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Descending("createdAt"),
                    new CreateIndexOptions { Name = "idx_createdAt", Background = true }
                ),
                
                // Index for creator filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("creator"),
                    new CreateIndexOptions { Name = "idx_creator", Background = true }
                ),
                
                // Index for name search
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Text("name").Text("description"),
                    new CreateIndexOptions { Name = "idx_text_search", Background = true }
                ),
                
                // Index for template filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("isTemplate"),
                    new CreateIndexOptions { Name = "idx_isTemplate", Background = true }
                ),
                
                // Index for tag filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("tags"),
                    new CreateIndexOptions { Name = "idx_tags", Background = true }
                ),
                
                // Index for public workflows
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
                // Index for username lookup
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("username"),
                    new CreateIndexOptions { Name = "idx_username_unique", Unique = true, Background = true }
                ),
                
                // Index for email lookup
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
                // Index for program ID lookup - critical for batch queries
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId"),
                    new CreateIndexOptions { Name = "idx_programId", Background = true }
                ),
                
                // Compound index for program and version number
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId").Descending("versionNumber"),
                    new CreateIndexOptions { Name = "idx_programId_versionNumber", Background = true }
                ),
                
                // Index for status filtering
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
                // Index for program ID lookup - critical for component counts
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId"),
                    new CreateIndexOptions { Name = "idx_programId", Background = true }
                ),
                
                // Compound index for program and version
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId").Ascending("programVersionId"),
                    new CreateIndexOptions { Name = "idx_programId_versionId", Background = true }
                ),
                
                // Index for status filtering
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("status"),
                    new CreateIndexOptions { Name = "idx_status", Background = true }
                )
            };

            await CreateIndexesSafelyAsync(collection, indexes, "uiComponents", cancellationToken);
        }

        // *** NEWLY ADDED METHOD ***
        private async Task CreateExecutionsIndexesAsync(CancellationToken cancellationToken)
        {
            var collection = _database.GetCollection<BsonDocument>("executions");

            var indexes = new List<CreateIndexModel<BsonDocument>>
            {
                // Index for program ID lookup - CRITICAL for performance of Program Statistics.
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("programId"),
                    new CreateIndexOptions { Name = "idx_programId", Background = true }
                )
            };

            await CreateIndexesSafelyAsync(collection, indexes, "executions", cancellationToken);
        }

        private async Task CreateIndexesSafelyAsync(
            IMongoCollection<BsonDocument> collection,
            List<CreateIndexModel<BsonDocument>> indexes,
            string collectionName,
            CancellationToken cancellationToken)
        {
            try
            {
                // Create indexes only if they don't exist (idempotent)
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
                // Don't throw here, continue with other collections
            }
        }
    }

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