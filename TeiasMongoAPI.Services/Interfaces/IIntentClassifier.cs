using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for classifying user intent from prompts
    /// This helps optimize context gathering by determining what files need to be read
    /// </summary>
    public interface IIntentClassifier
    {
        /// <summary>
        /// Classify the user's intent based on their prompt and project structure
        /// </summary>
        /// <param name="userPrompt">The user's prompt</param>
        /// <param name="projectStructure">Current project structure</param>
        /// <param name="currentlyOpenFiles">Files currently open in the editor (optional)</param>
        /// <param name="cursorContext">Current cursor position/selection (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Classified intent with required files to read</returns>
        Task<UserIntent> ClassifyIntentAsync(
            string userPrompt,
            ProjectStructureAnalysis projectStructure,
            List<string>? currentlyOpenFiles = null,
            string? cursorContext = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Select files to read based on intent and token budget
        /// </summary>
        /// <param name="intent">Classified intent</param>
        /// <param name="projectStructure">Project structure</param>
        /// <param name="maxTokens">Maximum tokens to use for file content</param>
        /// <returns>List of files to read, prioritized by importance</returns>
        List<string> SelectFilesForIntent(
            UserIntent intent,
            ProjectStructureAnalysis projectStructure,
            int maxTokens);
    }
}
