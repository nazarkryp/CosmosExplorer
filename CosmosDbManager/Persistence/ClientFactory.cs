using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.Azure.Cosmos;

namespace CosmosDbManager.Persistence
{
    internal sealed class ClientFactory
    {
        private Lazy<CosmosClient> _lazyClient;
        private bool _disposed;

        public ClientFactory(string connectionString)
        {
            _lazyClient = new Lazy<CosmosClient>(() => BuildClient(connectionString));
        }

        ~ClientFactory()
            => Dispose(false);

        public CosmosClient GetClient()
            => _lazyClient.Value;

        public async Task TryConnectAsync()
        {

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _lazyClient?.Value.Dispose();
                    _lazyClient = null;
                }

                _disposed = true;
            }
        }

        private static CosmosClient BuildClient(string connectionString)
        {
            return new CosmosClient(connectionString, new CosmosClientOptions
            {
                Serializer = new SystemTextJsonCosmosSerializer(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    Converters =
                    {
                        //new TimeSpanConverter(),
                        new JsonStringEnumConverter()
                    }
                }),
#if DEBUG
                // Ignore SSL errors for local CosmosDb Emulator
                HttpClientFactory = () => new HttpClient(new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                }),
                // Change ConnectionMode for local CosmosDb Emulator
                ConnectionMode = ConnectionMode.Gateway
#endif
            });
        }
    }

    public class SystemTextJsonCosmosSerializer : CosmosSerializer
    {
        private readonly JsonSerializerOptions _options;

        public SystemTextJsonCosmosSerializer(JsonSerializerOptions options)
        {
            _options = options;
        }

        public override T FromStream<T>(Stream stream)
        {
            using (stream)
            {
                // Would be more efficient if CosmosSerializer supported async
                using (var memory = new MemoryStream((int)stream.Length))
                {
                    stream.CopyTo(memory);

                    byte[] utf8Json = memory.ToArray();

                    return JsonSerializer.Deserialize<T>(utf8Json, _options);
                }
            }
        }

        public override Stream ToStream<T>(T input)
        {
            byte[] utf8Json = JsonSerializer.SerializeToUtf8Bytes(input, _options);

            return new MemoryStream(utf8Json);
        }
    }
}
