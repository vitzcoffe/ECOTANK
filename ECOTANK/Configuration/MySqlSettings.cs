using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECOTANK.Configuration
{
    /// <summary>
    /// Datos de conexión MySQL. Se cargan desde Resources/Raw/appsettings.json
    /// (empaquetado como MauiAsset). Rellena la contraseña en ese archivo.
    /// </summary>
    public sealed class MySqlSettings
    {
        public string Server { get; set; } = "localhost";
        public uint Port { get; set; } = 3306;
        public string Database { get; set; } = "ecotank";
        public string User { get; set; } = "root";
        public string Password { get; set; } = "";
        public string SslMode { get; set; } = "Preferred";

        private sealed class Root
        {
            [JsonPropertyName("MySql")]
            public MySqlSettings? MySql { get; set; }
        }

        /// <summary>Lee appsettings.json del paquete de la app.</summary>
        public static async Task<MySqlSettings> LoadAsync()
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("appsettings.json");
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var root = JsonSerializer.Deserialize<Root>(json, options);
                return root?.MySql ?? new MySqlSettings();
            }
            catch
            {
                // Si el archivo no existe o está mal formado, usa valores por defecto.
                return new MySqlSettings();
            }
        }
    }
}
