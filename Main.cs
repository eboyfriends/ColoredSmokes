using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using System.Drawing;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Entities;

namespace ColoredSmokes
{
    public class Main : BasePlugin, IPluginConfig<MainConfig>
    {
        public override string ModuleName => "ColoredSmokes";
        public override string ModuleVersion => "6.6.6";
        public override string ModuleAuthor => "eboyfriends";
        private MySqlConnection _connection = null!;
        private string _tableName = string.Empty;
        public required MainConfig Config { get; set; }

        private Dictionary<ulong, Color> PlayerColors = new();

        private readonly Dictionary<string, Color> PredefinedColors = new() {
            { "red", Color.Red },
            { "green", Color.Green },
            { "blue", Color.Blue },
            { "yellow", Color.Yellow },
            { "purple", Color.Purple },
            { "cyan", Color.Cyan },
            { "orange", Color.Orange },
            { "pink", Color.Pink },
            { "white", Color.White }
        };

        public override void Load(bool hotReload) {
            Logger.LogInformation("We are loading ColoredSmokes!");

            _connection = Config.DatabaseConfig.CreateConnection();
            _connection.Open();
            _tableName = Config.DatabaseConfig.Table;

            Task.Run(async () =>
            {
                await _connection.ExecuteAsync($@"
                    CREATE TABLE IF NOT EXISTS `{_tableName}` (
                        `steamid` BIGINT UNSIGNED NOT NULL,
                        `SelectedSmoke` VARCHAR(255) NOT NULL DEFAULT 'white',
                        PRIMARY KEY (`steamid`));");
            });

            RegisterListener<Listeners.OnEntityCreated>(OnEntityCreated);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        }

        public override void Unload(bool hotReload) {
            Logger.LogInformation("We are unloading ColoredSmokes!");
            
            _connection?.Dispose();
        }

        public void OnConfigParsed(MainConfig config) {
            Config = config;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info) {
            PlayerColors.Clear();
            return HookResult.Continue;
        }

        public void OnEntityCreated(CEntityInstance entity) {
            Server.NextFrame(() => {
                string designerName = entity.DesignerName;
                if (designerName != "smokegrenade_projectile") return;
                
                CSmokeGrenadeProjectile grenade = new CSmokeGrenadeProjectile(entity.Handle);
                if (!grenade.IsValid || grenade.AbsOrigin == null || grenade.AbsVelocity == null) return;

                CCSPlayerPawn? originalThrower = grenade.OriginalThrower.Value;
                CCSPlayerController? originalController = originalThrower?.OriginalController.Value;

                if (originalThrower == null || originalController == null || originalController.AuthorizedSteamID == null) return;

                if (PlayerColors.TryGetValue(originalController.AuthorizedSteamID.SteamId64, out Color color)) {
                    grenade.SmokeColor.X = color.R / 255.0f;
                    grenade.SmokeColor.Y = color.G / 255.0f;
                    grenade.SmokeColor.Z = color.B / 255.0f;
                }
            });
        }

        private void OnClientAuthorized(int playerSlot, SteamID steamId) {
            Task.Run(async () => {
                var result = await _connection.QueryFirstOrDefaultAsync<string>(
                    $"SELECT SelectedSmoke FROM {_tableName} WHERE steamid = @SteamId",
                    new { SteamId = steamId.SteamId64 });

                if (result != null && PredefinedColors.TryGetValue(result.ToLower(), out Color color)) {
                    Server.NextFrame(() => {
                        PlayerColors[steamId.SteamId64] = color;
                        var player = Utilities.GetPlayerFromSteamId(steamId.SteamId64);
                        if (player != null) {
                            player.PrintToChat($"Your smoke color ({result}) has been loaded.");
                        }
                    });
                }
            });
        }

        private void OnClientDisconnect(int playerSlot) {
            Server.NextFrame(() => {
                var player = Utilities.GetPlayerFromSlot(playerSlot);
                if (player != null && player.AuthorizedSteamID != null) {
                    PlayerColors.Remove(player.AuthorizedSteamID.SteamId64);
                }
            });
        }

        [ConsoleCommand("css_smoke", "Set your smoke color")]
        public async void OnCommandSmokeColor(CCSPlayerController? player, CommandInfo command) {
            if (player == null || player.AuthorizedSteamID == null) return;

            if (command.ArgCount < 2) {
                player.PrintToChat("Usage: /smoke <color or hex>");
                return;
            }

            string colorInput = command.ArgByIndex(1);
            Color color;

            if (PredefinedColors.TryGetValue(colorInput.ToLower(), out Color predefinedColor)) {
                color = predefinedColor;
            } else if (colorInput.StartsWith("#") && ColorTranslator.FromHtml(colorInput) != Color.Empty) {
                color = ColorTranslator.FromHtml(colorInput);
            } else {
                player.PrintToChat("Invalid color.");
                return;
            }

            PlayerColors[player.AuthorizedSteamID.SteamId64] = color;
            player.PrintToChat($"Your smoke color has been set to: {colorInput}");

            await _connection.ExecuteAsync(
                $"INSERT INTO {_tableName} (steamid, SelectedSmoke) VALUES (@SteamId, @Color) " +
                "ON DUPLICATE KEY UPDATE SelectedSmoke = @Color",
                new { SteamId = player.AuthorizedSteamID.SteamId64, Color = colorInput }
            );
        }
    }
}