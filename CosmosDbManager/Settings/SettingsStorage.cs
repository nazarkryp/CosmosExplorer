using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;

using Windows.Storage;

namespace CosmosDbManager.Settings;

public sealed class SettingsStorage
{
    //private const string SettingsFileName = "settings.txt";

    //private readonly StorageFolder _storageFolder = ApplicationData.Current.LocalFolder;

    public void SaveConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException();
        }

        var json = ApplicationData.Current.LocalSettings.Values["ConnectionStrings"] as string;

        var connectionStrings = !string.IsNullOrEmpty(json) ? JsonSerializer.Deserialize<List<string>>(json!)! : new List<string>();
        
        connectionStrings.Add(connectionString);

        ApplicationData.Current.LocalSettings.Values["ConnectionStrings"] = JsonSerializer.Serialize(connectionStrings);
    }

    public void DeleteConnectionString(string connectionString)
    {
        var json = ApplicationData.Current.LocalSettings.Values["ConnectionStrings"] as string;

        var connectionStrings = !string.IsNullOrEmpty(json) ? JsonSerializer.Deserialize<List<string>>(json!)! : new List<string>();

        connectionStrings.Remove(connectionString);

        ApplicationData.Current.LocalSettings.Values["ConnectionStrings"] = JsonSerializer.Serialize(connectionStrings);
    }

    public IReadOnlyCollection<string> GetConnectionStrings()
    {
        var json = ApplicationData.Current.LocalSettings.Values["ConnectionStrings"] as string;

        var connectionStrings = !string.IsNullOrEmpty(json) ? JsonSerializer.Deserialize<List<string>>(json!)! : new List<string>();

        return connectionStrings.AsReadOnly();
    }

    //public async Task<IReadOnlyCollection<string>> GetConnectionStrings()
    //{
    //    var storageFile = await _storageFolder.CreateFileAsync(SettingsFileName, CreationCollisionOption.OpenIfExists);

    //    var connectionStrings = await FileIO.ReadLinesAsync(storageFile);

    //    return connectionStrings.ToImmutableArray();
    //}
}
