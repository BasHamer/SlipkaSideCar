using Slipka.Preprocessors.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Slipka.ApiArguments
{
    /// <summary>
    /// API message for configuring preprocessors on a proxy session
    /// </summary>
    public class PreprocessorMessage
    {
        /// <summary>
        /// The type of preprocessor to create
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Configuration object specific to the preprocessor type
        /// </summary>
        public Dictionary<string, object> Config { get; set; }

        /// <summary>
        /// Converts this message to an IPreprocessor instance using the provided factory
        /// </summary>
        /// <param name="factory">The preprocessor factory to use for creation</param>
        /// <returns>The configured preprocessor instance</returns>
        public async Task<IPreprocessor> ToPreprocessorAsync(IPreprocessorFactory factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            return await factory.CreatePreprocessorAsync(this);
        }
    }
}
