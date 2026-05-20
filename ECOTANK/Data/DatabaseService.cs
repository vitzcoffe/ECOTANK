using ECOTANK.Configuration;
using MySqlConnector;
using System.Security.Cryptography;

namespace ECOTANK.Data
{
    /// <summary>
    /// Punto único de acceso a MySQL para ECOTANK.
    /// Construye el connection string desde appsettings.json, abre conexiones,
    /// prueba conectividad y crea las tablas ejecutando database/schema.sql.
    /// </summary>
    public sealed class DatabaseService
    {
        private MySqlSettings? _settings;

        private async Task<MySqlSettings> GetSettingsAsync()
            => _settings ??= await MySqlSettings.LoadAsync();

        private static MySqlSslMode ParseSslMode(string value) =>
            Enum.TryParse<MySqlSslMode>(value, ignoreCase: true, out var mode)
                ? mode
                : MySqlSslMode.Preferred;

        /// <summary>Connection string apuntando a la base de datos `ecotank`.</summary>
        private async Task<string> BuildConnectionStringAsync(bool includeDatabase = true)
        {
            var s = await GetSettingsAsync();
            var builder = new MySqlConnectionStringBuilder
            {
                Server = s.Server,
                Port = s.Port,
                UserID = s.User,
                Password = s.Password,
                SslMode = ParseSslMode(s.SslMode),
                ConnectionTimeout = 15,
                DefaultCommandTimeout = 60
            };

            if (includeDatabase)
                builder.Database = s.Database;

            return builder.ConnectionString;
        }

        /// <summary>Abre una conexión lista para usar contra la BD `ecotank`.</summary>
        public async Task<MySqlConnection> OpenConnectionAsync()
        {
            var connection = new MySqlConnection(await BuildConnectionStringAsync());
            await connection.OpenAsync();
            return connection;
        }

        /// <summary>
        /// Comprueba que el servidor responde. Devuelve (ok, mensaje).
        /// </summary>
        public async Task<(bool Ok, string Message)> TestConnectionAsync()
        {
            try
            {
                // Sin base de datos: valida credenciales aunque `ecotank` aún no exista.
                await using var connection =
                    new MySqlConnection(await BuildConnectionStringAsync(includeDatabase: false));
                await connection.OpenAsync();

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT VERSION();";
                var version = (string?)await cmd.ExecuteScalarAsync();

                return (true, $"Conexión MySQL OK (servidor {version}).");
            }
            catch (Exception ex)
            {
                return (false, $"No se pudo conectar a MySQL: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea la base de datos y todas las tablas ejecutando database/schema.sql.
        /// Idempotente (el script usa IF NOT EXISTS). No inserta datos.
        /// </summary>
        public async Task<(bool Ok, string Message)> InitializeDatabaseAsync()
        {
            try
            {
                string script;
                using (var stream = await FileSystem.OpenAppPackageFileAsync("schema.sql"))
                using (var reader = new StreamReader(stream))
                {
                    script = await reader.ReadToEndAsync();
                }

                if (string.IsNullOrWhiteSpace(script))
                    return (false, "schema.sql está vacío o no se encontró en el paquete.");

                // Conexión sin base de datos: el script ejecuta CREATE DATABASE + USE.
                await using var connection =
                    new MySqlConnection(await BuildConnectionStringAsync(includeDatabase: false));
                await connection.OpenAsync();

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = script;          // MySqlConnector ejecuta múltiples sentencias.
                await cmd.ExecuteNonQueryAsync();

                return (true, "Esquema aplicado: base de datos y tablas creadas.");
            }
            catch (Exception ex)
            {
                return (false, $"Error creando el esquema: {ex.Message}");
            }
        }

        /// <summary>
        /// Inserta los roles base de la plataforma si no existen.
        /// </summary>
        public async Task<(bool Ok, string Message)> EnsureDefaultRolesAsync()
        {
            try
            {
                var roles = new[]
                {
                    ("Turista", "Cliente que reserva alojamientos y actividades."),
                    ("Anfitrion", "Usuario que publica alojamientos."),
                    ("Administrador", "Usuario que gestiona la plataforma."),
                    ("Negocio Local", "Comercio asociado a experiencias turisticas.")
                };

                await using var connection = await OpenConnectionAsync();

                foreach (var (name, description) in roles)
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = """
                        INSERT INTO roles (nombre, descripcion)
                        VALUES (@name, @description)
                        ON DUPLICATE KEY UPDATE descripcion = VALUES(descripcion);
                        """;
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@description", description);
                    await cmd.ExecuteNonQueryAsync();
                }

                return (true, "Roles base listos.");
            }
            catch (Exception ex)
            {
                return (false, $"No se pudieron preparar los roles: {ex.Message}");
            }
        }

        public async Task<(bool Ok, string Message, int? UserId)> RegisterUserAsync(
            string fullName,
            string email,
            string password,
            string roleName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fullName))
                    return (false, "Escribe el nombre completo.", null);

                if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                    return (false, "Escribe un correo valido.", null);

                if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                    return (false, "La contrasena debe tener minimo 6 caracteres.", null);

                await using var connection = await OpenConnectionAsync();

                var roleId = await GetRoleIdAsync(connection, roleName);
                if (roleId is null)
                    return (false, $"No existe el rol {roleName}.", null);

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO usuarios (nombre_completo, email, password_hash, rol_id)
                    VALUES (@fullName, @email, @passwordHash, @roleId);
                    SELECT LAST_INSERT_ID();
                    """;
                cmd.Parameters.AddWithValue("@fullName", fullName.Trim());
                cmd.Parameters.AddWithValue("@email", email.Trim().ToLowerInvariant());
                cmd.Parameters.AddWithValue("@passwordHash", HashPassword(password));
                cmd.Parameters.AddWithValue("@roleId", roleId.Value);

                var userId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return (true, $"Usuario creado con ID {userId}.", userId);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return (false, "Ese correo ya esta registrado.", null);
            }
            catch (Exception ex)
            {
                return (false, $"No se pudo registrar el usuario: {ex.Message}", null);
            }
        }

        public async Task<(bool Ok, string Message)> ValidateLoginAsync(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    return (false, "Escribe correo y contrasena.");

                await using var connection = await OpenConnectionAsync();
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    SELECT u.nombre_completo, u.password_hash, r.nombre
                    FROM usuarios u
                    INNER JOIN roles r ON r.id = u.rol_id
                    WHERE u.email = @email AND u.activo = 1
                    LIMIT 1;
                    """;
                cmd.Parameters.AddWithValue("@email", email.Trim().ToLowerInvariant());

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return (false, "No encontramos un usuario activo con ese correo.");

                var fullName = reader.GetString(0);
                var passwordHash = reader.GetString(1);
                var roleName = reader.GetString(2);

                if (!VerifyPassword(password, passwordHash))
                    return (false, "La contrasena no coincide.");

                return (true, $"Bienvenido, {fullName}. Rol: {roleName}.");
            }
            catch (Exception ex)
            {
                return (false, $"No se pudo validar el login: {ex.Message}");
            }
        }

        private static async Task<int?> GetRoleIdAsync(MySqlConnection connection, string roleName)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id FROM roles WHERE nombre = @roleName LIMIT 1;";
            cmd.Parameters.AddWithValue("@roleName", roleName);

            var result = await cmd.ExecuteScalarAsync();
            return result is null ? null : Convert.ToInt32(result);
        }

        private static string HashPassword(string password)
        {
            const int saltSize = 16;
            const int hashSize = 32;
            const int iterations = 100_000;

            var salt = RandomNumberGenerator.GetBytes(saltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                hashSize);

            return $"pbkdf2-sha256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256")
                return false;

            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}
