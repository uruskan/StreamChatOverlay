using System.IO;
using System.Text.Json;

namespace StreamChatOverlay.Models;

public sealed class AppSettings
{
    public string TwitchUsername { get; set; } = "";
    public string KickUsername { get; set; } = "";
    public double FontSize { get; set; } = 14;
    public double Opacity { get; set; } = 1.0;
    public string BackgroundColor { get; set; } = "#00000000";
    public string TextColor { get; set; } = "#FFFFFF";
    public bool ShowPlatformIcon { get; set; } = true;
    public bool ShowBadges { get; set; } = true;
    public bool ShowEmotes { get; set; } = true;
    public int MaxMessages { get; set; } = 200;
    public bool MessageFadeEnabled { get; set; } = false;
    public int MessageFadeSeconds { get; set; } = 30;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public double WindowWidth { get; set; } = 350;
    public double WindowHeight { get; set; } = 600;
    public string NotificationSound { get; set; } = "None";
    public double NotificationVolume { get; set; } = 0.5;

    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new AppSettings();
        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
