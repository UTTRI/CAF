using System.Text.Json;

namespace CellphoneProcessor;

/// <summary>
/// ViewModels will read this class for loading in settings
/// </summary>
public sealed class Configuration
{
    public static Configuration Shared { get; private set; } = null!;

    private readonly Dictionary<string, Dictionary<string, string>> _parameters = new();
    private readonly string _configFile;

    /// <summary>
    /// Generate a new instance of the configuration file from
    /// the 
    /// </summary>
    /// <param name="configFile">The configuration file name.</param>
    private Configuration(string configFile)
    {
        _configFile = configFile;
        if (!File.Exists(configFile))
        {
            return;
        }
       
        [DoesNotReturn]
        static string ThrowInvalidConfig()
        {
            throw new InvalidDataException("Invalid configuration file!");
        }

        static bool ReadPastComments(ref Utf8JsonReader reader)
        {
            while(reader.Read())
            {
                if(reader.TokenType != JsonTokenType.Comment)
                {
                    return true;
                }
            }
            return false;
        }

        Utf8JsonReader reader = new(File.ReadAllBytes(configFile));
        while (reader.Read())
        {
            if(reader.TokenType == JsonTokenType.PropertyName)
            {
                var subsystem = reader.GetString() ?? ThrowInvalidConfig();
                var parameters = new Dictionary<string, string>();
                _parameters[subsystem] = parameters;
                if (!ReadPastComments(ref reader)) ThrowInvalidConfig();
                if (reader.TokenType != JsonTokenType.StartObject) ThrowInvalidConfig();
                string? parameterName = null;
                while(ReadPastComments(ref reader) && reader.TokenType != JsonTokenType.EndObject)
                {
                    switch(reader.TokenType)
                    {
                        case JsonTokenType.PropertyName:
                            parameterName = reader.GetString() ?? ThrowInvalidConfig();
                            break;
                        case JsonTokenType.String:
                            if (parameterName is null) ThrowInvalidConfig();
                            parameters[parameterName] = reader.GetString() ?? ThrowInvalidConfig();
                            parameterName = null;
                            break;
                        default:
                            ThrowInvalidConfig();
                            break;
                    }                   
                }
            }
        }
    }

    /// <summary>
    /// Initialize the configuration and set it up as the Shared configuration.
    /// </summary>
    /// <param name="configFile">The file name to use.</param>
    public static void Initialize(string configFile)
    {
        var config = new Configuration(configFile);
        Shared = config;
    }

    /// <summary>
    /// Save the configuration to its file path.
    /// </summary>
    public void Save()
    {
        // make sure that only one call to save can happen at the same time
        lock (this)
        {
            using var stream = new BufferedStream(File.OpenWrite(_configFile));
            Utf8JsonWriter writer = new(stream);
            writer.WriteStartObject();
            foreach (var parameter in _parameters)
            {
                writer.WritePropertyName(parameter.Key);
                writer.WriteStartObject();
                foreach (var subsystemEntry in parameter.Value)
                {
                    writer.WriteString(subsystemEntry.Key, subsystemEntry.Value);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// get the parameters for a given subsystem.
    /// </summary>
    /// <param name="subSystemKey">The name of the subsystem to lookup</param>
    /// <returns>The dictionary of parameters for the subsystem.</returns>
    internal Dictionary<string, string> GetParameters(string subSystemKey)
    {
        // If we don't have the subsystem loaded, create an empty
        // dictionary for the parameters.
        lock (this)
        {
            if (!_parameters.TryGetValue(subSystemKey, out var parameters))
            {
                _parameters[subSystemKey] =
                    (parameters = new Dictionary<string, string>());
            }
            return parameters;
        }
    }

    /// <summary>
    /// Only updates the given string to set
    /// if the key was found within the dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary to draw from</param>
    /// <param name="key">The key to lookup.</param>
    /// <param name="toSet">The value to update if the value was found.</param>
    public static void TryUpdateValue(Dictionary<string, string> dictionary, string key,
        ref string toSet)
    {
        if (dictionary.TryGetValue(key, out string? temp))
        {
            toSet = temp;
        }
    }
}
