using Microsoft.Extensions.Hosting;
using Slipka.Configuration;
using Slipka.Preprocessors.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slipka
{
    /// <summary>
    /// Background service that initializes custom preprocessors at application startup.
    /// </summary>
    public class PreprocessorInitializationService : IHostedService
    {
        private readonly IPreprocessorRegistry _registry;
        private readonly IPreprocessorFactory _factory;
        private readonly PreprocessorSettings _settings;

        public PreprocessorInitializationService(
            IPreprocessorRegistry registry,
            IPreprocessorFactory factory,
            PreprocessorSettings settings)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Initializing custom preprocessors...");

            try
            {
                // Load custom preprocessor assemblies
                if (_settings.CustomPreprocessorAssemblies.Count > 0)
                {
                    await _registry.LoadPreprocessorTypesFromAssembliesAsync(
                        _settings.CustomPreprocessorAssemblies.ToArray());

                    Console.WriteLine($"Loaded {_settings.CustomPreprocessorAssemblies.Count} custom preprocessor assemblies");
                }

                // Register custom preprocessor types from configuration
                foreach (var kvp in _settings.CustomPreprocessorTypes)
                {
                    try
                    {
                        var type = Type.GetType(kvp.Value);
                        if (type != null)
                        {
                            _registry.RegisterPreprocessorType(kvp.Key, type);
                            Console.WriteLine($"Registered custom preprocessor type: {kvp.Key} -> {kvp.Value}");
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Could not load type '{kvp.Value}' for preprocessor '{kvp.Key}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error registering preprocessor type '{kvp.Key}': {ex.Message}");
                    }
                }

                // If we have custom types registered, update the factory
                if (_factory is Preprocessors.PreprocessorFactory concreteFactory)
                {
                    foreach (var typeName in _registry.GetRegisteredTypeNames())
                    {
                        var type = _registry.GetPreprocessorType(typeName);
                        if (type != null)
                        {
                            concreteFactory.RegisterPreprocessorType(typeName, async (message) =>
                            {
                                // Create instance using reflection
                                var instance = Activator.CreateInstance(type);
                                if (instance is IPreprocessor preprocessor)
                                {
                                    // TODO: Add configuration support for custom preprocessors
                                    // For now, just return the instance
                                    return preprocessor;
                                }
                                throw new InvalidOperationException($"Type {type.FullName} does not implement IPreprocessor");
                            });
                        }
                    }
                }

                var supportedTypes = _factory.GetSupportedTypes();
                Console.WriteLine($"Preprocessor initialization complete. Supported types: {string.Join(", ", supportedTypes)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing preprocessors: {ex.Message}");
                // Don't throw - allow the application to continue without custom preprocessors
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Cleanup if needed
            return Task.CompletedTask;
        }
    }
}
