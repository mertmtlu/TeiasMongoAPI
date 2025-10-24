using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// High-level semantic search service
    /// Orchestrates code chunking, embedding generation, and vector storage/search
    /// </summary>
    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly ICodeChunker _codeChunker;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorStore _vectorStore;
        private readonly EmbeddingSettings _embeddingSettings;
        private readonly ILogger<SemanticSearchService> _logger;

        public SemanticSearchService(
            ICodeChunker codeChunker,
            IEmbeddingService embeddingService,
            IVectorStore vectorStore,
            IOptions<EmbeddingSettings> embeddingSettings,
            ILogger<SemanticSearchService> logger)
        {
            _codeChunker = codeChunker;
            _embeddingService = embeddingService;
            _vectorStore = vectorStore;
            _embeddingSettings = embeddingSettings.Value;
            _logger = logger;
        }

        public async Task<IndexingResult> IndexProjectAsync(
            string programId,
            string versionId,
            bool forceReindex = false,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new IndexingResult { Success = false };

            try
            {
                _logger.LogInformation("Starting indexing for program {ProgramId}, version {VersionId}, forceReindex: {ForceReindex}",
                    programId, versionId, forceReindex);

                // Check if already indexed
                var exists = await _vectorStore.CollectionExistsAsync(programId, versionId, cancellationToken);
                if (exists && !forceReindex)
                {
                    _logger.LogInformation("Collection already exists and forceReindex is false. Skipping indexing.");
                    result.Warnings.Add("Collection already exists. Use forceReindex=true to recreate.");
                    result.Success = true;
                    result.Duration = stopwatch.Elapsed;
                    return result;
                }

                // Delete existing collection if force reindex
                if (exists && forceReindex)
                {
                    _logger.LogInformation("Force reindex requested. Deleting existing collection.");
                    await _vectorStore.DeleteCollectionAsync(programId, versionId, cancellationToken);
                }

                // Step 1: Chunk the project
                _logger.LogInformation("Step 1/4: Chunking project code...");
                var chunks = await _codeChunker.ChunkProjectAsync(programId, versionId, cancellationToken);

                if (chunks.Count == 0)
                {
                    result.ErrorMessage = "No chunks created. Project may be empty or all files failed to process.";
                    result.Duration = stopwatch.Elapsed;
                    return result;
                }

                result.ChunksCreated = chunks.Count;
                _logger.LogInformation("Created {ChunkCount} chunks", chunks.Count);

                // Step 2: Generate embeddings
                _logger.LogInformation("Step 2/4: Generating embeddings...");
                var embeddings = await GenerateEmbeddingsForChunksAsync(chunks, cancellationToken);
                result.EmbeddingsGenerated = embeddings.Count;
                _logger.LogInformation("Generated {EmbeddingCount} embeddings", embeddings.Count);

                // Attach embeddings to chunks
                for (int i = 0; i < Math.Min(chunks.Count, embeddings.Count); i++)
                {
                    chunks[i].Embedding = embeddings[i];
                    chunks[i].EmbeddingModel = _embeddingService.GetModelName();
                    chunks[i].EmbeddingDimension = _embeddingService.GetEmbeddingDimensions();
                }

                // Step 3: Create vector collection
                _logger.LogInformation("Step 3/4: Creating vector collection...");
                await _vectorStore.CreateOrUpdateCollectionAsync(
                    programId,
                    versionId,
                    _embeddingService.GetEmbeddingDimensions(),
                    cancellationToken
                );

                // Step 4: Store embeddings
                _logger.LogInformation("Step 4/4: Storing embeddings in vector database...");
                await _vectorStore.UpsertChunksAsync(programId, versionId, chunks, cancellationToken);

                result.Success = true;
                result.FilesProcessed = chunks.Select(c => c.FilePath).Distinct().Count();
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                _logger.LogInformation("Indexing completed successfully in {Duration}. Files: {Files}, Chunks: {Chunks}, Embeddings: {Embeddings}",
                    result.Duration, result.FilesProcessed, result.ChunksCreated, result.EmbeddingsGenerated);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index project {ProgramId}, version {VersionId}", programId, versionId);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = stopwatch.Elapsed;
                return result;
            }
        }

        public async Task<List<VectorSearchResult>> SearchCodeAsync(
            string programId,
            string versionId,
            string query,
            int topK = 10,
            Dictionary<string, object>? filters = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Searching code for query: '{Query}' in program {ProgramId}, version {VersionId}",
                    query, programId, versionId);

                // Check if collection exists
                var exists = await _vectorStore.CollectionExistsAsync(programId, versionId, cancellationToken);
                if (!exists)
                {
                    _logger.LogWarning("Collection does not exist for program {ProgramId}, version {VersionId}. Returning empty results.",
                        programId, versionId);
                    return new List<VectorSearchResult>();
                }

                // Generate embedding for query
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

                // Search vector store
                var results = await _vectorStore.SearchSimilarAsync(
                    programId,
                    versionId,
                    queryEmbedding,
                    topK,
                    filters,
                    cancellationToken
                );

                _logger.LogInformation("Found {Count} results for query '{Query}'", results.Count, query);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search code for query '{Query}'", query);
                throw;
            }
        }

        public async Task<List<VectorSearchResult>> FindSimilarCodeAsync(
            string programId,
            string versionId,
            string chunkId,
            int topK = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Finding similar code to chunk {ChunkId} in program {ProgramId}, version {VersionId}",
                    chunkId, programId, versionId);

                // First, get the chunk's embedding by searching for it
                // This is a simplified approach - ideally we'd have a GetChunk method
                var allResults = await _vectorStore.SearchSimilarAsync(
                    programId,
                    versionId,
                    new float[_embeddingService.GetEmbeddingDimensions()], // Dummy vector
                    topK: 1000, // Large limit to find our chunk
                    cancellationToken: cancellationToken
                );

                var targetChunk = allResults.FirstOrDefault(r => r.Chunk.Id == chunkId);
                if (targetChunk == null || targetChunk.Chunk.Embedding == null)
                {
                    _logger.LogWarning("Chunk {ChunkId} not found or has no embedding", chunkId);
                    return new List<VectorSearchResult>();
                }

                // Search using the chunk's embedding
                var results = await _vectorStore.SearchSimilarAsync(
                    programId,
                    versionId,
                    targetChunk.Chunk.Embedding,
                    topK + 1, // +1 because the chunk itself will be in results
                    cancellationToken: cancellationToken
                );

                // Remove the target chunk from results
                results = results.Where(r => r.Chunk.Id != chunkId).Take(topK).ToList();

                _logger.LogInformation("Found {Count} similar chunks to {ChunkId}", results.Count, chunkId);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find similar code for chunk {ChunkId}", chunkId);
                throw;
            }
        }

        public async Task<bool> IsProjectIndexedAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _vectorStore.CollectionExistsAsync(programId, versionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if project is indexed");
                return false;
            }
        }

        public async Task DeleteProjectIndexAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Deleting index for program {ProgramId}, version {VersionId}", programId, versionId);

                await _vectorStore.DeleteCollectionAsync(programId, versionId, cancellationToken);

                _logger.LogInformation("Index deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete project index");
                throw;
            }
        }

        public async Task<IndexStats?> GetIndexStatsAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var collectionStats = await _vectorStore.GetCollectionStatsAsync(programId, versionId, cancellationToken);
                if (collectionStats == null)
                {
                    return null;
                }

                return new IndexStats
                {
                    ProgramId = programId,
                    VersionId = versionId,
                    TotalChunks = collectionStats.VectorCount,
                    IndexedChunks = collectionStats.IndexedVectorCount,
                    CreatedAt = collectionStats.CreatedAt,
                    LastUpdatedAt = collectionStats.LastUpdatedAt,
                    IsIndexing = collectionStats.IsIndexing,
                    EmbeddingModel = _embeddingService.GetModelName(),
                    EmbeddingDimensions = _embeddingService.GetEmbeddingDimensions()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get index stats");
                return null;
            }
        }

        #region Private Helper Methods

        private async Task<List<float[]>> GenerateEmbeddingsForChunksAsync(
            List<CodeChunk> chunks,
            CancellationToken cancellationToken)
        {
            try
            {
                // Extract text content from chunks
                var texts = chunks.Select(chunk =>
                {
                    // Create a rich text representation including metadata
                    var contextPrefix = !string.IsNullOrEmpty(chunk.ParentContext)
                        ? $"[Context: {chunk.ParentContext}] "
                        : "";

                    var typePrefix = chunk.Name != null
                        ? $"[{chunk.Type}: {chunk.Name}] "
                        : $"[{chunk.Type}] ";

                    return $"{_embeddingSettings.TitlePrefix}{contextPrefix}{typePrefix}{chunk.Content}";
                }).ToList();

                // Generate embeddings in batch
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken);

                return embeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embeddings for chunks");
                throw;
            }
        }

        #endregion
    }
}
