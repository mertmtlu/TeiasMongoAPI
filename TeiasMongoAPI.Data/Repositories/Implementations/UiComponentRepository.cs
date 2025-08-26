using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class UiComponentRepository : GenericRepository<UiComponent>, IUiComponentRepository
    {
        private readonly MongoDbContext _context;
        private readonly IMongoCollection<UiComponent> _collection;

        public UiComponentRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
            _collection = _context.Database.GetCollection<UiComponent>("uicomponents");
        }

        #region Version-specific Component Operations

        public async Task<IEnumerable<UiComponent>> GetByProgramVersionAsync(ObjectId programId, ObjectId versionId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, programId),
                Builders<UiComponent>.Filter.Eq(c => c.VersionId, versionId)
            );

            return await _collection
                .Find(filter)
                .SortBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> IsNameUniqueForVersionAsync(ObjectId programId, ObjectId versionId, string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, programId),
                Builders<UiComponent>.Filter.Eq(c => c.VersionId, versionId),
                Builders<UiComponent>.Filter.Eq(c => c.Name, name)
            );

            if (excludeId.HasValue)
            {
                filter = Builders<UiComponent>.Filter.And(
                    filter,
                    Builders<UiComponent>.Filter.Ne(c => c._ID, excludeId.Value)
                );
            }

            var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            return count == 0;
        }

        #endregion

        #region Program-level Component Operations

        public async Task<IEnumerable<UiComponent>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            return await _collection
                .Find(c => c.ProgramId == programId)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        #endregion

        #region Component Filtering and Discovery

        public async Task<IEnumerable<UiComponent>> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
        {
            return await _collection
                .Find(c => c.Type == type)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UiComponent>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default)
        {
            return await _collection
                .Find(c => c.Creator == creatorId)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UiComponent>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return await _collection
                .Find(c => c.Status == status)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        #endregion

        #region Component Lifecycle Management

        public async Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default)
        {
            var update = Builders<UiComponent>.Update.Set(c => c.Status, status);
            var result = await _collection.UpdateOneAsync(
                c => c._ID == id,
                update,
                cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        #endregion

        #region Component Search and Filtering

        public async Task<IEnumerable<UiComponent>> SearchComponentsAsync(
            string? name = null,
            string? type = null,
            string? creator = null,
            string? status = null,
            ObjectId? programId = null,
            ObjectId? versionId = null,
            List<string>? tags = null,
            DateTime? createdFrom = null,
            DateTime? createdTo = null,
            CancellationToken cancellationToken = default)
        {
            var filterBuilder = Builders<UiComponent>.Filter;
            var filters = new List<FilterDefinition<UiComponent>>();

            if (!string.IsNullOrEmpty(name))
            {
                filters.Add(filterBuilder.Regex(c => c.Name, new BsonRegularExpression(name, "i")));
            }

            if (!string.IsNullOrEmpty(type))
            {
                filters.Add(filterBuilder.Eq(c => c.Type, type));
            }

            if (!string.IsNullOrEmpty(creator))
            {
                filters.Add(filterBuilder.Eq(c => c.Creator, creator));
            }

            if (!string.IsNullOrEmpty(status))
            {
                filters.Add(filterBuilder.Eq(c => c.Status, status));
            }

            if (programId.HasValue)
            {
                filters.Add(filterBuilder.Eq(c => c.ProgramId, programId.Value));
            }

            if (versionId.HasValue)
            {
                filters.Add(filterBuilder.Eq(c => c.VersionId, versionId.Value));
            }

            if (tags?.Any() == true)
            {
                filters.Add(filterBuilder.AnyIn(c => c.Tags, tags));
            }

            if (createdFrom.HasValue)
            {
                filters.Add(filterBuilder.Gte(c => c.CreatedAt, createdFrom.Value));
            }

            if (createdTo.HasValue)
            {
                filters.Add(filterBuilder.Lte(c => c.CreatedAt, createdTo.Value));
            }

            var finalFilter = filters.Any()
                ? filterBuilder.And(filters)
                : filterBuilder.Empty;

            return await _collection
                .Find(finalFilter)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        #endregion

        #region Component Statistics and Analytics

        public async Task<int> GetComponentCountForProgramAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            return (int)await _collection.CountDocumentsAsync(
                c => c.ProgramId == programId,
                cancellationToken: cancellationToken);
        }

        public async Task<int> GetComponentCountForVersionAsync(ObjectId programId, ObjectId versionId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, programId),
                Builders<UiComponent>.Filter.Eq(c => c.VersionId, versionId)
            );

            return (int)await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        }

        public async Task<int> GetComponentCountByTypeAsync(string type, CancellationToken cancellationToken = default)
        {
            return (int)await _collection.CountDocumentsAsync(
                c => c.Type == type,
                cancellationToken: cancellationToken);
        }

        public async Task<int> GetActiveComponentCountAsync(CancellationToken cancellationToken = default)
        {
            return (int)await _collection.CountDocumentsAsync(
                c => c.Status == "active",
                cancellationToken: cancellationToken);
        }

        #endregion

        #region Component Version Management

        public async Task<IEnumerable<UiComponent>> GetComponentsForCopyingAsync(ObjectId fromProgramId, ObjectId fromVersionId, List<string>? componentNames = null, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, fromProgramId),
                Builders<UiComponent>.Filter.Eq(c => c.VersionId, fromVersionId)
            );

            if (componentNames?.Any() == true)
            {
                filter = Builders<UiComponent>.Filter.And(
                    filter,
                    Builders<UiComponent>.Filter.In(c => c.Name, componentNames)
                );
            }

            return await _collection
                .Find(filter)
                .SortBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasComponentsInVersionAsync(ObjectId programId, ObjectId versionId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, programId),
                Builders<UiComponent>.Filter.Eq(c => c.VersionId, versionId)
            );

            var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            return count > 0;
        }

        #endregion

        #region Component Validation and Compatibility

        public async Task<IEnumerable<UiComponent>> GetCompatibleComponentsAsync(string programType, string? programLanguage = null, List<string>? requiredFeatures = null, CancellationToken cancellationToken = default)
        {
            var filterBuilder = Builders<UiComponent>.Filter;
            var filters = new List<FilterDefinition<UiComponent>>
            {
                filterBuilder.Eq(c => c.Status, "active")
            };

            // Basic compatibility based on program type
            if (programType == "web")
            {
                filters.Add(filterBuilder.Regex(c => c.Type, new BsonRegularExpression("web|component|ui", "i")));
            }

            // Language-specific compatibility
            if (!string.IsNullOrEmpty(programLanguage))
            {
                var languageFilter = filterBuilder.Or(
                    filterBuilder.Regex(c => c.Type, new BsonRegularExpression(programLanguage, "i")),
                    filterBuilder.AnyIn(c => c.Tags, new[] { programLanguage.ToLowerInvariant() })
                );
                filters.Add(languageFilter);
            }

            // Feature-based compatibility
            if (requiredFeatures?.Any() == true)
            {
                var featureFilter = filterBuilder.AnyIn(c => c.Tags, requiredFeatures);
                filters.Add(featureFilter);
            }

            var finalFilter = filterBuilder.And(filters);

            return await _collection
                .Find(finalFilter)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UiComponent>> GetRecommendedComponentsAsync(ObjectId programId, ObjectId excludeVersionId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, programId),
                Builders<UiComponent>.Filter.Ne(c => c.VersionId, excludeVersionId),
                Builders<UiComponent>.Filter.Eq(c => c.Status, "active")
            );

            return await _collection
                .Find(filter)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        #endregion

        #region Component Tags and Categories

        public async Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default)
        {
            var pipeline = new[]
            {
                new BsonDocument("$unwind", "$Tags"),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$Tags" }
                }),
                new BsonDocument("$sort", new BsonDocument("_id", 1))
            };

            var result = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
            return result.Select(doc => doc["_id"].AsString).Where(tag => !string.IsNullOrEmpty(tag));
        }

        public async Task<IEnumerable<UiComponent>> GetByTagsAsync(List<string> tags, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.AnyIn(c => c.Tags, tags);

            return await _collection
                .Find(filter)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<string, int>> GetComponentCountByTypeAsync(CancellationToken cancellationToken = default)
        {
            var pipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$Type" },
                    { "count", new BsonDocument("$sum", 1) }
                }),
                new BsonDocument("$sort", new BsonDocument("_id", 1))
            };

            var result = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
            return result.ToDictionary(
                doc => doc["_id"].AsString ?? "unknown",
                doc => doc["count"].AsInt32
            );
        }

        #endregion

        #region Latest Active Component for Python UI Generation

        public async Task<UiComponent?> GetLatestActiveByProgramAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, programId),
                Builders<UiComponent>.Filter.Eq(c => c.Status, "active")
            );

            return await _collection
                .Find(filter)
                .SortByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        #endregion

        #region Batch Operations for Performance

        public async Task<Dictionary<string, int>> GetComponentCountsByProgramIdsAsync(IEnumerable<ObjectId> programIds, CancellationToken cancellationToken = default)
        {
            var programIdsList = programIds.ToList();
            if (!programIdsList.Any())
                return new Dictionary<string, int>();

            var pipeline = new BsonDocument[]
            {
                new BsonDocument("$match", new BsonDocument("ProgramId", new BsonDocument("$in", new BsonArray(programIdsList)))),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$ProgramId" },
                    { "count", new BsonDocument("$sum", 1) }
                })
            };

            var cursor = await _collection.AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken);
            var results = await cursor.ToListAsync(cancellationToken);

            var componentCounts = new Dictionary<string, int>();
            
            // Initialize all program IDs with 0 count
            foreach (var programId in programIdsList)
            {
                componentCounts[programId.ToString()] = 0;
            }

            // Update with actual counts from the aggregation
            foreach (var result in results)
            {
                var programId = result["_id"].AsObjectId.ToString();
                var count = result["count"].AsInt32;
                componentCounts[programId] = count;
            }

            return componentCounts;
        }

        public async Task<Dictionary<string, string?>> GetNewestComponentTypesByProgramIdsAsync(IEnumerable<ObjectId> programIds, CancellationToken cancellationToken = default)
        {
            var programIdsList = programIds.ToList();
            if (!programIdsList.Any())
                return new Dictionary<string, string?>();

            var pipeline = new BsonDocument[]
            {
                new BsonDocument("$match", new BsonDocument("ProgramId", new BsonDocument("$in", new BsonArray(programIdsList)))),
                new BsonDocument("$sort", new BsonDocument
                {
                    { "ProgramId", 1 },
                    { "CreatedAt", -1 }
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$ProgramId" },
                    { "newestComponentType", new BsonDocument("$first", "$Type") }
                })
            };

            var cursor = await _collection.AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken);
            var results = await cursor.ToListAsync(cancellationToken);

            var newestComponentTypes = new Dictionary<string, string?>();
            
            // Initialize all program IDs with null
            foreach (var programId in programIdsList)
            {
                newestComponentTypes[programId.ToString()] = null;
            }

            // Update with actual newest component types from the aggregation
            foreach (var result in results)
            {
                var programId = result["_id"].AsObjectId.ToString();
                var componentType = result.Contains("newestComponentType") && !result["newestComponentType"].IsBsonNull 
                    ? result["newestComponentType"].AsString 
                    : null;
                newestComponentTypes[programId] = componentType;
            }

            return newestComponentTypes;
        }

        #endregion
    }
}