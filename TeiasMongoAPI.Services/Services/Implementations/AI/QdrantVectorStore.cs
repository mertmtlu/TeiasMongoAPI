using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Qdrant vector store implementation for storing and querying code embeddings
    /// Uses gRPC client for optimal performance
    /// </summary>
    public class QdrantVectorStore : IVectorStore, IDisposable
    {
        private readonly QdrantClient _client;
        private readonly QdrantSettings _settings;
        private readonly ILogger<QdrantVectorStore> _logger;
        private bool _disposed = false;

        public QdrantVectorStore(
            IOptions<VectorStoreSettings> settings,
            ILogger<QdrantVectorStore> logger)
        {
            _settings = settings.Value.Qdrant;
            _logger = logger;

            // Initialize Qdrant client
            var host = _settings.Host;
            var port = _settings.GrpcPort;

            _logger.LogInformation("Initializing Qdrant client for {Host}:{Port}", host, port);

            try
            {
                // Create client with gRPC (more performant than HTTP)
                _client = new QdrantClient(
                    host: host,
                    port: port,
                    https: _settings.UseTls,
                    apiKey: _settings.ApiKey
                );

                _logger.LogInformation("Qdrant client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Qdrant client");
                throw;
            }
        }

        public async Task CreateOrUpdateCollectionAsync(
            string programId,
            string versionId,
            int dimensions,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId, versionId);

            try
            {
                _logger.LogInformation("Creating/updating collection: {CollectionName} with {Dimensions} dimensions",
                    collectionName, dimensions);

                // Check if collection exists
                var collections = await _client.ListCollectionsAsync(cancellationToken: cancellationToken);
                var exists = collections.Any(c => c == collectionName);

                if (exists)
                {
                    _logger.LogInformation("Collection {CollectionName} already exists", collectionName);
                    return;
                }

                // Create collection with vector configuration
                await _client.CreateCollectionAsync(
                    collectionName: collectionName,
                    vectorsConfig: new VectorParams
                    {
                        Size = (ulong)dimensions,
                        Distance = Distance.Cosine // Cosine similarity for semantic search
                    },
                    cancellationToken: cancellationToken
                );

                // Store metadata point with index creation timestamp
                var metadataPoint = new PointStruct
                {
                    Id = new PointId { Uuid = "metadata_index_info" },
                    Vectors = new float[dimensions], // Zero vector
                    Payload =
                    {
                        ["isMetadata"] = "true", // Store as string
                        ["indexedAt"] = DateTime.UtcNow.ToString("O"),
                        ["programId"] = programId,
                        ["versionId"] = versionId
                    }
                };

                await _client.UpsertAsync(collectionName, new[] { metadataPoint }, cancellationToken: cancellationToken);

                _logger.LogInformation("Collection {CollectionName} created successfully", collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create collection {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task<bool> CollectionExistsAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId, versionId);

            try
            {
                var collections = await _client.ListCollectionsAsync(cancellationToken: cancellationToken);
                return collections.Any(c => c == collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if collection {CollectionName} exists", collectionName);
                return false;
            }
        }

        public async Task UpsertChunksAsync(
            string programId,
            string versionId,
            List<CodeChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            if (chunks == null || chunks.Count == 0)
            {
                _logger.LogWarning("Attempted to upsert empty chunks list");
                return;
            }

            var collectionName = GetCollectionName(programId, versionId);

            try
            {
                _logger.LogInformation("Upserting {Count} chunks to collection {CollectionName}",
                    chunks.Count, collectionName);

                // Filter chunks that have embeddings
                var chunksWithEmbeddings = chunks.Where(c => c.Embedding != null && c.Embedding.Length > 0).ToList();
                if (chunksWithEmbeddings.Count != chunks.Count)
                {
                    _logger.LogWarning("Filtered out {Count} chunks without embeddings",
                        chunks.Count - chunksWithEmbeddings.Count);
                }

                if (chunksWithEmbeddings.Count == 0)
                {
                    _logger.LogWarning("No chunks with embeddings to upsert");
                    return;
                }

                // Convert chunks to Qdrant points
                var points = chunksWithEmbeddings.Select(chunk => new PointStruct
                {
                    Id = new PointId { Uuid = chunk.Id },
                    Vectors = chunk.Embedding!,
                    Payload =
                    {
                        ["chunkId"] = chunk.Id,
                        ["programId"] = chunk.ProgramId,
                        ["versionId"] = chunk.VersionId,
                        ["filePath"] = chunk.FilePath,
                        ["chunkType"] = chunk.Type.ToString(),
                        ["name"] = chunk.Name ?? "",
                        ["content"] = chunk.Content,
                        ["startLine"] = chunk.StartLine,
                        ["endLine"] = chunk.EndLine,
                        ["language"] = chunk.Language ?? "",
                        ["parentContext"] = chunk.ParentContext ?? "",
                        ["contentHash"] = chunk.ContentHash ?? "",
                        ["createdAt"] = chunk.CreatedAt.ToString("O"),
                        ["embeddingModel"] = chunk.EmbeddingModel ?? "",
                        ["embeddingDimension"] = chunk.EmbeddingDimension ?? 0
                    }
                }).ToList();

                // Add custom metadata (convert to strings for Qdrant)
                for (int i = 0; i < chunksWithEmbeddings.Count; i++)
                {
                    var chunk = chunksWithEmbeddings[i];
                    if (chunk.Metadata != null)
                    {
                        foreach (var (key, value) in chunk.Metadata)
                        {
                            points[i].Payload[$"meta_{key}"] = value?.ToString() ?? "";
                        }
                    }
                }

                // Upsert points (batch upload)
                await _client.UpsertAsync(
                    collectionName: collectionName,
                    points: points,
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("Successfully upserted {Count} points to {CollectionName}",
                    points.Count, collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upsert chunks to collection {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task<List<VectorSearchResult>> SearchSimilarAsync(
            string programId,
            string versionId,
            float[] queryEmbedding,
            int topK = 10,
            Dictionary<string, object>? filters = null,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId, versionId);

            try
            {
                _logger.LogDebug("Searching for {TopK} similar vectors in {CollectionName}", topK, collectionName);

                // Build filter conditions
                Filter? filter = null;
                if (filters != null && filters.Count > 0)
                {
                    var conditions = new List<Condition>();
                    foreach (var (key, value) in filters)
                    {
                        conditions.Add(new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = key,
                                Match = new Match { Text = value.ToString() }
                            }
                        });
                    }

                    filter = new Filter
                    {
                        Must = { conditions }
                    };
                }

                // Perform search
                var searchResult = await _client.SearchAsync(
                    collectionName: collectionName,
                    vector: queryEmbedding,
                    limit: (ulong)topK,
                    filter: filter,
                    scoreThreshold: 0.5f, // Minimum similarity threshold
                    cancellationToken: cancellationToken
                );

                // Convert results to VectorSearchResult
                var results = new List<VectorSearchResult>();
                int rank = 1;

                foreach (var scoredPoint in searchResult)
                {
                    var payload = scoredPoint.Payload;

                    var chunk = new CodeChunk
                    {
                        Id = payload["chunkId"].StringValue,
                        ProgramId = payload["programId"].StringValue,
                        VersionId = payload["versionId"].StringValue,
                        FilePath = payload["filePath"].StringValue,
                        Type = Enum.Parse<ChunkType>(payload["chunkType"].StringValue),
                        Name = payload["name"].StringValue,
                        Content = payload["content"].StringValue,
                        StartLine = (int)payload["startLine"].IntegerValue,
                        EndLine = (int)payload["endLine"].IntegerValue,
                        Language = payload["language"].StringValue,
                        ParentContext = payload.ContainsKey("parentContext") ? payload["parentContext"].StringValue : null,
                        ContentHash = payload.ContainsKey("contentHash") ? payload["contentHash"].StringValue : null,
                        EmbeddingModel = payload.ContainsKey("embeddingModel") ? payload["embeddingModel"].StringValue : null,
                        EmbeddingDimension = payload.ContainsKey("embeddingDimension") ? (int)payload["embeddingDimension"].IntegerValue : null
                    };

                    // Parse createdAt
                    if (payload.ContainsKey("createdAt"))
                    {
                        DateTime.TryParse(payload["createdAt"].StringValue, out var createdAt);
                        chunk.CreatedAt = createdAt;
                    }

                    // Extract metadata
                    foreach (var (key, value) in payload)
                    {
                        if (key.StartsWith("meta_"))
                        {
                            var metaKey = key.Substring(5);
                            chunk.Metadata[metaKey] = value;
                        }
                    }

                    results.Add(new VectorSearchResult
                    {
                        Chunk = chunk,
                        Score = scoredPoint.Score,
                        Rank = rank++
                    });
                }

                _logger.LogInformation("Found {Count} similar vectors in {CollectionName}", results.Count, collectionName);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search in collection {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task DeleteChunkAsync(
            string programId,
            string versionId,
            string chunkId,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId, versionId);

            try
            {
                _logger.LogInformation("Deleting chunk {ChunkId} from {CollectionName}", chunkId, collectionName);

                await _client.DeleteAsync(
                    collectionName: collectionName,
                    ids: new[] { new PointId { Uuid = chunkId } },
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("Chunk {ChunkId} deleted successfully", chunkId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete chunk {ChunkId}", chunkId);
                throw;
            }
        }

        public async Task DeleteCollectionAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId, versionId);

            try
            {
                _logger.LogInformation("Deleting collection {CollectionName}", collectionName);

                await _client.DeleteCollectionAsync(
                    collectionName: collectionName,
                    cancellationToken: cancellationToken
                );

                _logger.LogInformation("Collection {CollectionName} deleted successfully", collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete collection {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task<CollectionStats?> GetCollectionStatsAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId, versionId);

            try
            {
                var info = await _client.GetCollectionInfoAsync(
                    collectionName: collectionName,
                    cancellationToken: cancellationToken
                );

                return new CollectionStats
                {
                    VectorCount = (long)info.VectorsCount,
                    IndexedVectorCount = (long)info.IndexedVectorsCount,
                    SizeBytes = 0, // Qdrant doesn't provide size directly
                    IsIndexing = info.Status == CollectionStatus.Yellow,
                    CreatedAt = DateTime.UtcNow, // Qdrant doesn't provide creation time
                    LastUpdatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get stats for collection {CollectionName}", collectionName);
                return null;
            }
        }

        public async Task<DateTime?> GetIndexTimestampAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId, versionId);

            try
            {
                // Search for the metadata point
                var zeroVector = new float[768]; // Assuming 768 dims - could be made dynamic
                var results = await _client.SearchAsync(
                    collectionName: collectionName,
                    vector: zeroVector,
                    limit: 1,
                    filter: new Filter
                    {
                        Must =
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "isMetadata",
                                    Match = new Match { Keyword = "true" } // Store as string
                                }
                            }
                        }
                    },
                    cancellationToken: cancellationToken
                );

                if (results.Any())
                {
                    var metadataPoint = results.First();
                    if (metadataPoint.Payload.ContainsKey("indexedAt"))
                    {
                        var timestampStr = metadataPoint.Payload["indexedAt"].StringValue;
                        if (DateTime.TryParse(timestampStr, out var timestamp))
                        {
                            return timestamp;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get index timestamp for {CollectionName}", collectionName);
                return null;
            }
        }

        private string GetCollectionName(string programId, string versionId)
        {
            // Format: {prefix}{programId}_{versionId}
            // Qdrant collection names must be alphanumeric with underscores
            var sanitized = $"{programId}_{versionId}".Replace("-", "_").ToLowerInvariant();
            return $"{_settings.CollectionPrefix}{sanitized}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _client?.Dispose();
                _disposed = true;
            }
        }
    }
}
