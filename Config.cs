using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using MySqlConnector;

namespace ColoredSmokes {
    public class MainConfig : BasePluginConfig {

        public DatabaseConfig DatabaseConfig { get; set; } = new DatabaseConfig();
    }

    public class DatabaseConfig : BasePluginConfig {
        [JsonPropertyName("host")]
		public string Host { get; set; } = "localhost";

		[JsonPropertyName("username")]
		public string Username { get; set; } = "root";

		[JsonPropertyName("database")]
		public string Database { get; set; } = "cs2";

		[JsonPropertyName("password")]
		public string Password { get; set; } = "password";

		[JsonPropertyName("port")]
		public int Port { get; set; } = 3306;
        
		[JsonPropertyName("table")]
		public string Table { get; set; } = "ColoredSmokes";

		[JsonPropertyName("sslmode")]
		public string Sslmode { get; set; } = "none";

        public MySqlConnection CreateConnection() {
            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = Host,
                UserID = Username,
                Password = Password,
                Database = Database,
                Port = (uint)Port,
                SslMode = Enum.Parse<MySqlSslMode>(Sslmode, true),
            };

            return new MySqlConnection(builder.ToString());
        }
    }
}

