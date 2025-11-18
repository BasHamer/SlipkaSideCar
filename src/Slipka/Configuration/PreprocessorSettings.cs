using System.Collections.Generic;

namespace Slipka.Configuration
{
    /// <summary>
    /// Configuration settings for custom preprocessors.
    /// </summary>
    public class PreprocessorSettings
    {
        /// <summary>
        /// Paths to assemblies containing custom preprocessor implementations.
        /// These assemblies will be loaded at startup and their preprocessor types registered.
        /// </summary>
        public List<string> CustomPreprocessorAssemblies { get; set; } = new List<string>();

        /// <summary>
        /// Custom preprocessor type registrations.
        /// Maps preprocessor type names to their fully qualified .NET type names.
        /// </summary>
        public Dictionary<string, string> CustomPreprocessorTypes { get; set; } = new Dictionary<string, string>();
    }
}
