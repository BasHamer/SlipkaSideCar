using System;
using System.Threading.Tasks;

namespace Slipka.Preprocessors.Interfaces
{
    /// <summary>
    /// Registry interface for managing custom preprocessor types.
    /// Allows registration of custom preprocessor implementations and discovery of available types.
    /// </summary>
    public interface IPreprocessorRegistry
    {
        /// <summary>
        /// Registers a custom preprocessor type.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type</param>
        /// <param name="preprocessorType">The .NET type that implements IPreprocessor</param>
        /// <exception cref="ArgumentException">Thrown when the type is invalid or already registered</exception>
        void RegisterPreprocessorType(string typeName, Type preprocessorType);

        /// <summary>
        /// Unregisters a custom preprocessor type.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type to unregister</param>
        /// <returns>True if the type was unregistered, false if it wasn't found</returns>
        bool UnregisterPreprocessorType(string typeName);

        /// <summary>
        /// Checks if a preprocessor type is registered.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type</param>
        /// <returns>True if the type is registered, false otherwise</returns>
        bool IsPreprocessorTypeRegistered(string typeName);

        /// <summary>
        /// Gets all registered preprocessor type names.
        /// </summary>
        /// <returns>Array of registered preprocessor type names</returns>
        string[] GetRegisteredTypeNames();

        /// <summary>
        /// Gets the .NET type for a registered preprocessor type name.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type</param>
        /// <returns>The .NET type, or null if not found</returns>
        Type GetPreprocessorType(string typeName);

        /// <summary>
        /// Loads and registers preprocessor types from assemblies.
        /// </summary>
        /// <param name="assemblyPaths">Paths to assemblies containing preprocessor types</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task LoadPreprocessorTypesFromAssembliesAsync(string[] assemblyPaths);
    }
}
