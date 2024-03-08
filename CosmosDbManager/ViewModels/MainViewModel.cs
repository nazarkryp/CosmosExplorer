using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using CosmosDbManager.Infrastructure;
using CosmosDbManager.Persistence;
using CosmosDbManager.Settings;

namespace CosmosDbManager.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private readonly SettingsStorage _settingsStorage = new();

    private CancellationTokenSource? _cancellationTokenSource;
    private CosmosDbProvider? _cosmosDbProvider;
    private string? _connectionString;

    private ObservableCollection<string> _connectionStrings;

    private string? _query;
    private string? _limit;
    private string? _outputLog;
    private string? _database;
    private string? _container;
    private bool _hasDatabases;
    private bool _hasContainers;
    private bool _isContainerSelected;
    private string? _statusOutput;
    private ObservableCollection<CosmosDatabase> _databases = new();
    private ObservableCollection<CosmosContainer> _containers = new();
    private CosmosResponse<dynamic> _cosmosResponse = new();

    #endregion

    public MainViewModel()
    {
        var connectionStrings = _settingsStorage.GetConnectionStrings();

        _connectionStrings = new ObservableCollection<string>(connectionStrings);
        _connectionString = _connectionStrings.FirstOrDefault();
    }

    #region Commands

    private AsyncCommand? _saveConnectionStringCommand;
    private AsyncCommand? _deleteConnectionStringCommand;
    private AsyncCommand? _connectCommand;
    private AsyncCommand? _executeCommand;
    private AsyncCommand? _cancelExecutionCommand;

    public AsyncCommand SaveConnectionStringCommand => _saveConnectionStringCommand ??= new AsyncCommand(() =>
    {
        var connectionString = _connectionString!.TrimEnd(';');

        _settingsStorage.SaveConnectionString(connectionString);

        ConnectionStrings.Add(connectionString);

        return Task.CompletedTask;
    }, () =>
    {
        return !string.IsNullOrEmpty(_connectionString) &&
               IsValidConnectionString(_connectionString!) &&
               !_connectionStrings.Contains(_connectionString!.TrimEnd(';'));
    });

    public AsyncCommand DeleteConnectionStringCommand => _deleteConnectionStringCommand ??= new AsyncCommand(() =>
    {
        var connectionString = _connectionString!.TrimEnd(';');

        _settingsStorage.DeleteConnectionString(connectionString);

        ConnectionStrings.Remove(connectionString);

        return Task.CompletedTask;
    }, () => {
        return !string.IsNullOrEmpty(_connectionString) && _connectionStrings.Contains(_connectionString!.TrimEnd(';'));
    });

    public AsyncCommand ConnectCommand => _connectCommand ??= new AsyncCommand(async () =>
    {
        Reset();

        if (string.IsNullOrEmpty(_connectionString))
        {
            return;
        }

        OutputLog = $"Connecting to\n\"{_connectionString}\"\n\n";

        if (_cosmosDbProvider is not null)
        {
            _cosmosDbProvider.Dispose();
            _cosmosDbProvider = null;
        }

        _cosmosDbProvider = new CosmosDbProvider(_connectionString!);

        var databases = await _cosmosDbProvider.FetchDatabasesAsync();

        Databases = new ObservableCollection<CosmosDatabase>(databases);

        HasDatabases = databases.Count > 0;

        OutputLog += $"Loaded {databases.Count} database(s)\n\n";
    }, () => !string.IsNullOrEmpty(_connectionString));

    public AsyncCommand ExecuteCommand => _executeCommand ??= new AsyncCommand(ExecuteAsync, () => !string.IsNullOrEmpty(Container));

    public AsyncCommand CancelExecutionCommand => _cancelExecutionCommand ??= new AsyncCommand(() =>
    {
        _cancellationTokenSource?.Cancel();

        return Task.CompletedTask;
    }, () => !string.IsNullOrEmpty(Container));

    #endregion

    #region Public Properties

    public string? ConnectionString
    {
        get => _connectionString;
        set
        {
            if (value == _connectionString)
            {
                return;
            }

            _connectionString = value;

            OnPropertyChanged();

            ConnectCommand.RaiseCanExecuteChanged();
            SaveConnectionStringCommand.RaiseCanExecuteChanged();
            DeleteConnectionStringCommand.RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<string> ConnectionStrings
    {
        get => _connectionStrings;
        set
        {
            if (Equals(value, _connectionStrings))
            {
                return;
            }

            _connectionStrings = value;

            OnPropertyChanged();
        }
    }

    public string? Limit
    {
        get => _limit;
        set
        {
            if (value == _limit)
            {
                return;
            }

            _limit = value;

            OnPropertyChanged();
        }
    }

    public string? Query
    {
        get => _query;
        set
        {
            if (value == _query)
            {
                return;
            }

            _query = value;

            OnPropertyChanged();
        }
    }

    public string? Database
    {
        get => _database;
        set
        {
            if (value == _database)
            {
                return;
            }

            _database = value;

            OnPropertyChanged();
        }
    }

    public bool HasDatabases
    {
        get => _hasDatabases;
        set
        {
            if (value == _hasDatabases)
            {
                return;
            }

            _hasDatabases = value;

            OnPropertyChanged();
        }
    }

    public string? Container
    {
        get => _container;
        set
        {
            if (value == _container)
            {
                return;
            }

            _container = value;
            IsContainerSelected = _container is not null;

            OnPropertyChanged();
        }
    }

    public bool HasContainers
    {
        get => _hasContainers;
        set
        {
            if (value == _hasContainers)
            {
                return;
            }

            _hasContainers = value;

            OnPropertyChanged();
        }
    }

    public bool IsContainerSelected
    {
        get => _isContainerSelected;
        set
        {
            if (value == _isContainerSelected)
            {
                return;
            }

            _isContainerSelected = value;

            OnPropertyChanged();
        }
    }

    public string? OutputLog
    {
        get => _outputLog;
        set
        {
            if (value == _outputLog)
            {
                return;
            }

            _outputLog = value;

            OnPropertyChanged();
        }
    }

    public string? StatusOutput
    {
        get => _statusOutput;
        set
        {
            if (value == _statusOutput)
            {
                return;
            }

            _statusOutput = value;

            OnPropertyChanged();
        }
    }

    public ObservableCollection<CosmosDatabase> Databases
    {
        get => _databases;
        set
        {
            if (Equals(value, _databases))
            {
                return;
            }

            _databases = value;

            OnPropertyChanged();
        }
    }

    public ObservableCollection<CosmosContainer> Containers
    {
        get => _containers;
        set
        {
            if (Equals(value, _containers))
            {
                return;
            }

            _containers = value;

            OnPropertyChanged();
        }
    }

    #endregion

    #region Public Methods


    public async Task SelectDatabaseAsync(CosmosDatabase cosmosDatabase)
    {
        Database = cosmosDatabase.Id;

        OutputLog += $"Retrieving '{cosmosDatabase.Id}' containers\n";

        var containers = await _cosmosDbProvider!.FetchContainersAsync(cosmosDatabase.Id);

        Containers = new ObservableCollection<CosmosContainer>(containers);

        OutputLog += $"Loaded {containers.Count} container(s)\n\n";

        HasContainers = containers.Count > 0;
    }

    public Task SelectContainerAsync(CosmosContainer cosmosContainer)
    {
        Container = cosmosContainer.Id;

        ExecuteCommand.RaiseCanExecuteChanged();
        CancelExecutionCommand.RaiseCanExecuteChanged();

        OutputLog += $"'{cosmosContainer.Id}' selected\n";

        return Task.CompletedTask;
    }

    #endregion

    #region Private Methods

    private async Task ExecuteAsync()
    {
        CancelExecutionCommand.RaiseCanExecuteChanged();

        if (_cancellationTokenSource is not null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        _cancellationTokenSource = new CancellationTokenSource();

        OutputLog = "Loading Documents";
        StatusOutput = null;

        var builder = new StringBuilder();
        var watch = Stopwatch.StartNew();

        try
        {
            var limit = int.TryParse(_limit, out var value) && value > 0 ? value : 10;

            var response = await _cosmosDbProvider!.FetchDocumentsAsync<dynamic>(_query, _database!, _container!, limit,
                _cosmosResponse.ContinuationToken, _cancellationTokenSource.Token);

            _cosmosResponse.Items = response.Items;
            _cosmosResponse.ContinuationToken = response.ContinuationToken;
            _cosmosResponse.RequestCharge = response.RequestCharge;

            //builder.AppendLine($"Loaded: {_cosmosCollection.Items.Count} documents");
            //builder.AppendLine();

            var json = JsonSerializer.Serialize(_cosmosResponse.Items, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            builder.AppendLine(json);
        }
        catch (Exception ex)
        {
            builder.AppendLine(ex.Message);
        }
        finally
        {
            watch.Stop();

            //builder.AppendLine($"Operation duration: {watch.Elapsed:g}");

            StatusOutput = $"Execution time: {watch.Elapsed:g}. Request Charge: {_cosmosResponse.RequestCharge} RU/s. Loaded: {_cosmosResponse.Items.Count} documents";
        }

        OutputLog = builder.ToString();
    }

    private void Reset()
    {
        StatusOutput = null;
        Databases.Clear();
        Containers.Clear();

        HasDatabases = false;
        HasContainers = false;
        Database = null;
        Container = null;
        OutputLog = null;

        ExecuteCommand.RaiseCanExecuteChanged();
    }

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        _cosmosDbProvider?.Dispose();
    }

    #endregion

    #region Helper Methods

    private static bool IsValidConnectionString(string input) => Regex.IsMatch(input, @"^AccountEndpoint=https:\/\/[a-zA-Z0-9\-]+\.documents\.azure\.com:443\/;AccountKey=[a-zA-Z0-9+=\/]+;?$");

    #endregion
}