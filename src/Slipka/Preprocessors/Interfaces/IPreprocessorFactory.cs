using Slipka.ApiArguments;
using System.Threading.Tasks;

namespace Slipka.Preprocessors.Interfaces
{
    /// <summary>
    /// Factory interface for creating preprocessor instances from configuration messages.
    /// This allows for extensible preprocessor creation, supporting both built-in and custom preprocessor types.
    /// </summary>
    public interface IPreprocessorFactory
    {
        /// <summary>
        /// Creates a preprocessor instance from the provided configuration message.
        /// </summary>
        /// <param name="message">The preprocessor configuration message</param>
        /// <returns>The configured preprocessor instance</returns>
        /// <exception cref="PreprocessorCreationException">Thrown when the preprocessor cannot be created</exception>
        Task<IPreprocessor> CreatePreprocessorAsync(PreprocessorMessage message);

        /// <summary>
        /// Checks if the factory can create a preprocessor of the specified type.
        /// </summary>
        /// <param name="preprocessorType">The preprocessor type to check</param>
        /// <returns>True if the factory supports this type, false otherwise</returns>
        bool CanCreatePreprocessor(string preprocessorType);

        /// <summary>
        /// Gets all preprocessor types supported by this factory.
        /// </summary>
        /// <returns>Array of supported preprocessor type names</returns>
        string[] GetSupportedTypes();
    }

    /// <summary>
    /// Exception thrown when a preprocessor cannot be created from configuration.
    /// </summary>
    public class PreprocessorCreationException : System.Exception
    {
        public PreprocessorCreationException(string message) : base(message) { }
        public PreprocessorCreationException(string message, System.Exception innerException)
            : base(message, innerException) { }
    }
}
