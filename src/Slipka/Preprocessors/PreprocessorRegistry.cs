using Slipka.Preprocessors.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Slipka.Preprocessors
{
    /// <summary>
    /// Default implementation of IPreprocessorRegistry for managing custom preprocessor types.
    /// </summary>
    public class PreprocessorRegistry : IPreprocessorRegistry
    {
        private readonly Dictionary<string, Type> _registeredTypes;

        /// <summary>
        /// Initializes a new instance of the PreprocessorRegistry.
        /// </summary>
        public PreprocessorRegistry()
        {
            _registeredTypes = new Dictionary<string, Type>();
        }

        /// <summary>
        /// Registers a custom preprocessor type.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type</param>
        /// <param name="preprocessorType">The .NET type that implements IPreprocessor</param>
        /// <exception cref="ArgumentException">Thrown when the type is invalid or already registered</exception>
        public void RegisterPreprocessorType(string typeName, Type preprocessorType)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                throw new ArgumentException("Type name cannot be null or empty", nameof(typeName));
            }

            if (preprocessorType == null)
            {
                throw new ArgumentNullException(nameof(preprocessorType));
            }

            if (!typeof(IPreprocessor).IsAssignableFrom(preprocessorType))
            {
                throw new ArgumentException($"Type {preprocessorType.FullName} does not implement IPreprocessor", nameof(preprocessorType));
            }

            var typeKey = typeName.ToLower();
            if (_registeredTypes.ContainsKey(typeKey))
            {
                throw new ArgumentException($"Preprocessor type '{typeName}' is already registered", nameof(typeName));
            }

            _registeredTypes[typeKey] = preprocessorType;
        }

        /// <summary>
        /// Unregisters a custom preprocessor type.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type to unregister</param>
        /// <returns>True if the type was unregistered, false if it wasn't found</returns>
        public bool UnregisterPreprocessorType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            var typeKey = typeName.ToLower();
            return _registeredTypes.Remove(typeKey);
        }

        /// <summary>
        /// Checks if a preprocessor type is registered.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type</param>
        /// <returns>True if the type is registered, false otherwise</returns>
        public bool IsPreprocessorTypeRegistered(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return _registeredTypes.ContainsKey(typeName.ToLower());
        }

        /// <summary>
        /// Gets all registered preprocessor type names.
        /// </summary>
        /// <returns>Array of registered preprocessor type names</returns>
        public string[] GetRegisteredTypeNames()
        {
            return _registeredTypes.Keys.ToArray();
        }

        /// <summary>
        /// Gets the .NET type for a registered preprocessor type name.
        /// </summary>
        /// <param name="typeName">The name of the preprocessor type</param>
        /// <returns>The .NET type, or null if not found</returns>
        public Type GetPreprocessorType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var typeKey = typeName.ToLower();
            return _registeredTypes.TryGetValue(typeKey, out var type) ? type : null;
        }

        /// <summary>
        /// Loads and registers preprocessor types from assemblies.
        /// </summary>
        /// <param name="assemblyPaths">Paths to assemblies containing preprocessor types</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task LoadPreprocessorTypesFromAssembliesAsync(string[] assemblyPaths)
        {
            if (assemblyPaths == null || assemblyPaths.Length == 0)
            {
                return;
            }

            await Task.Run(() =>
            {
                foreach (var assemblyPath in assemblyPaths)
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(assemblyPath);
                        RegisterPreprocessorTypesFromAssembly(assembly);
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other assemblies
                        Console.WriteLine($"Failed to load preprocessor types from assembly '{assemblyPath}': {ex.Message}");
                    }
                }
            });
        }

        private void RegisterPreprocessorTypesFromAssembly(Assembly assembly)
        {
            var preprocessorTypes = assembly.GetTypes()
                .Where(t => typeof(IPreprocessor).IsAssignableFrom(t) &&
                           !t.IsAbstract &&
                           !t.IsInterface &&
                           t.GetConstructor(Type.EmptyTypes) != null) // Must have parameterless constructor
                .ToArray();

            foreach (var type in preprocessorTypes)
            {
                // Use the class name as the default type name, but allow override via attribute
                var typeName = GetPreprocessorTypeName(type);

                if (!string.IsNullOrEmpty(typeName) && !_registeredTypes.ContainsKey(typeName.ToLower()))
                {
                    _registeredTypes[typeName.ToLower()] = type;
                }
            }
        }

        private string GetPreprocessorTypeName(Type type)
        {
            // Check for a custom attribute that specifies the type name
            var attribute = type.GetCustomAttribute<PreprocessorTypeNameAttribute>();
            if (attribute != null && !string.IsNullOrEmpty(attribute.TypeName))
            {
                return attribute.TypeName;
            }

            // Default to the class name
            return type.Name;
        }
    }

    /// <summary>
    /// Attribute to specify a custom type name for a preprocessor class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class PreprocessorTypeNameAttribute : Attribute
    {
        public string TypeName { get; }

        public PreprocessorTypeNameAttribute(string typeName)
        {
            TypeName = typeName;
        }
    }
}
