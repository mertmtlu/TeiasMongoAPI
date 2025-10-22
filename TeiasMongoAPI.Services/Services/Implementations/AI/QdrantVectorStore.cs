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
    /// Qdrant implementation of vector store for code embeddings
    /// </summary>
    public class QdrantVectorStore : IVectorStore
    {
        private readonly VectorStoreOptions _options;
        private readonly ILogger<QdrantVectorStore> _logger;
        private readonly QdrantClient _client;

        public QdrantVectorStore(
            IOptions<VectorStoreOptions> options,
            ILogger<QdrantVectorStore> logger)
        {
            _options = options.Value;
            _logger = logger;

            // Initialize Qdrant client
            // Parse the URL to extract host and port
            var uri = new Uri(_options.QdrantUrl);
            var host = uri.Host;
            var port = uri.Port;
            var https = uri.Scheme == "https";

            _client = new QdrantClient(
                host: host,
                port: port,
                https: https,
                apiKey: _options.QdrantApiKey
            );

            _logger.LogInformation("QdrantVectorStore initialized with host: {Host}:{Port} (HTTPS: {Https})",
                host, port, https);
        }

        public async Task EnsureCollectionExistsAsync(string programId, int dimension, CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId);

            try
            {
                // Check if collection exists
                var collections = await _client.ListCollectionsAsync(cancellationToken);
                var exists = collections.Any(c => c == collectionName);

                if (!exists)
                {
                    _logger.LogInformation("Creating Qdrant collection: {CollectionName} with dimension {Dimension}",
                        collectionName, dimension);

                    await _client.CreateCollectionAsync(
                        collectionName,
                        new VectorParams
                        {
                            Size = (ulong)dimension,
                            Distance = Distance.Cosine
                        },
                        cancellationToken: cancellationToken);

                    _logger.LogInformation("Collection {CollectionName} created successfully", collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure collection exists: {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task UpsertChunkAsync(CodeChunk chunk, CancellationToken cancellationToken = default)
        {
            await UpsertChunksAsync(new List<CodeChunk> { chunk }, cancellationToken);
        }

        public async Task UpsertChunksAsync(List<CodeChunk> chunks, CancellationToken cancellationToken = default)
        {
            if (!chunks.Any())
                return;

            var programId = chunks.First().ProgramId;
            var collectionName = GetCollectionName(programId);

            try
            {
                var points = chunks.Select(chunk => new PointStruct
                {
                    Id = new PointId { Uuid = chunk.Id },
                    Vectors = chunk.Embedding ?? throw new InvalidOperationException($"Chunk {chunk.Id} has no embedding"),
                    Payload =
                    {
                        ["program_id"] = programId,
                        ["version_id"] = chunk.VersionId,
                        ["file_path"] = chunk.FilePath,
                        ["chunk_type"] = chunk.Type.ToString(),
                        ["name"] = chunk.Name ?? "",
                        ["content"] = chunk.Content,
                        ["start_line"] = chunk.StartLine,
                        ["end_line"] = chunk.EndLine,
                        ["language"] = chunk.Language ?? "",
                        ["parent_context"] = chunk.ParentContext ?? "",
                        ["content_hash"] = chunk.ContentHash ?? "",
                        ["created_at"] = chunk.CreatedAt.ToString("O"),
                        ["embedding_model"] = chunk.EmbeddingModel ?? "unknown",
                        ["embedding_dimension"] = chunk.EmbeddingDimension ?? 0
                    }
                }).ToList();

                await _client.UpsertAsync(collectionName, points, cancellationToken: cancellationToken);

                _logger.LogInformation("Upserted {Count} chunks to collection {CollectionName}",
                    chunks.Count, collectionName);
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
            int topK = 20,
            double minimumScore = 0.5,
            CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId);

            try
            {
                var searchResult = await _client.SearchAsync(
                    collectionName,
                    queryEmbedding,
                    limit: (ulong)topK,
                    filter: new Filter
                    {
                        Must =
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "version_id",
                                    Match = new Match { Keyword = versionId }
                                }
                            }
                        }
                    },
                    scoreThreshold: (float)minimumScore,
                    cancellationToken: cancellationToken);

                var results = searchResult.Select((point, index) => new VectorSearchResult
                {
                    Chunk = new CodeChunk
                    {
                        Id = point.Id.Uuid,
                        ProgramId = point.Payload["program_id"].StringValue,
                        VersionId = point.Payload["version_id"].StringValue,
                        FilePath = point.Payload["file_path"].StringValue,
                        Type = Enum.Parse<ChunkType>(point.Payload["chunk_type"].StringValue),
                        Name = point.Payload["name"].StringValue,
                        Content = point.Payload["content"].StringValue,
                        StartLine = (int)point.Payload["start_line"].IntegerValue,
                        EndLine = (int)point.Payload["end_line"].IntegerValue,
                        Language = point.Payload["language"].StringValue,
                        ParentContext = point.Payload["parent_context"].StringValue,
                        ContentHash = point.Payload["content_hash"].StringValue,
                        EmbeddingModel = point.Payload.ContainsKey("embedding_model") ? point.Payload["embedding_model"].StringValue : null,
                        EmbeddingDimension = point.Payload.ContainsKey("embedding_dimension") ? (int)point.Payload["embedding_dimension"].IntegerValue : null
                    },
                    Score = point.Score,
                    Rank = index + 1
                }).ToList();

                _logger.LogInformation("Found {Count} similar chunks in collection {CollectionName} with scores above {MinScore}",
                    results.Count, collectionName, minimumScore);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search collection {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task DeleteVersionAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId);

            try
            {
                await _client.DeleteAsync(
                    collectionName,
                    filter: new Filter
                    {
                        Must =
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "version_id",
                                    Match = new Match { Keyword = versionId }
                                }
                            }
                        }
                    },
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Deleted all chunks for version {VersionId} from collection {CollectionName}",
                    versionId, collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete version {VersionId} from collection {CollectionName}",
                    versionId, collectionName);
                throw;
            }
        }

        public async Task DeleteCollectionAsync(string programId, CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId);

            try
            {
                await _client.DeleteCollectionAsync(collectionName, cancellationToken: cancellationToken);

                _logger.LogInformation("Deleted collection {CollectionName}", collectionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete collection {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task<int> GetChunkCountAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            var collectionName = GetCollectionName(programId);

            try
            {
                var countResult = await _client.CountAsync(
                    collectionName,
                    filter: new Filter
                    {
                        Must =
                        {
                            new Condition
                            {
                                Field = new FieldCondition
                                {
                                    Key = "version_id",
                                    Match = new Match { Keyword = versionId }
                                }
                            }
                        }
                    },
                    cancellationToken: cancellationToken);

                return (int)countResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get chunk count for collection {CollectionName}", collectionName);
                throw;
            }
        }

        public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.ListCollectionsAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Qdrant health check failed");
                return false;
            }
        }

        private string GetCollectionName(string programId)
        {
            // Sanitize program ID for use in collection name
            var sanitized = programId.Replace("-", "_").Replace(" ", "_");
            return $"{_options.CollectionPrefix}_{sanitized}";
        }
    }
}
