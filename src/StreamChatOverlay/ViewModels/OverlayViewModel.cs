using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamChatOverlay.Models;
using StreamChatOverlay.Services;

namespace StreamChatOverlay.ViewModels;

public partial class OverlayViewModel : ObservableObject
{
    private readonly TwitchChatService _twitchService = new();
    private readonly KickChatService _kickService = new();
    private readonly EmoteResolver _emoteResolver = new();
    private readonly SoundService _soundService = new();
    private readonly HashSet<string> _recentMessageIds = new();

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isBorderVisible = true;
    [ObservableProperty] private string _statusText = "Disconnected";
    [ObservableProperty] private AppSettings _settings;

    public OverlayViewModel()
    {
        _settings = AppSettings.Load();
        _soundService.SetSound(_settings.NotificationSound);

        _twitchService.OnMessageReceived += HandleMessage;
        _twitchService.OnError += err =>
            Application.Current.Dispatcher.InvokeAsync(() => StatusText = $"Twitch error: {err}");
        _twitchService.OnConnected += () =>
            Application.Current.Dispatcher.InvokeAsync(() => StatusText = "Twitch connected");
        _twitchService.OnDisconnected += () =>
            Application.Current.Dispatcher.InvokeAsync(() => StatusText = "Twitch disconnected");

        _kickService.OnMessageReceived += HandleMessage;
        _kickService.OnError += err =>
            Application.Current.Dispatcher.InvokeAsync(() => StatusText = $"Kick error: {err}");
        _kickService.OnConnected += () =>
            Application.Current.Dispatcher.InvokeAsync(() => StatusText = "Kick connected");
        _kickService.OnDisconnected += () =>
            Application.Current.Dispatcher.InvokeAsync(() => StatusText = "Kick disconnected");
    }

    private void HandleMessage(ChatMessage msg)
    {
        // Deduplicate messages
        if (!_recentMessageIds.Add(msg.Id))
            return;

        // Keep the set from growing unbounded
        if (_recentMessageIds.Count > 500)
            _recentMessageIds.Clear();

        // Resolve third-party emotes for Twitch messages
        if (msg.Platform == ChatPlatform.Twitch && Settings.ShowEmotes)
        {
            msg = msg with
            {
                Fragments = _emoteResolver.ResolveThirdPartyEmotes(msg.Fragments)
            };
        }

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Messages.Add(msg);
            while (Messages.Count > Settings.MaxMessages)
                Messages.RemoveAt(0);
            _soundService.Play(Settings.NotificationVolume);
        });
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var tasks = new List<Task>();

        if (!string.IsNullOrWhiteSpace(Settings.TwitchUsername))
        {
            StatusText = "Connecting to Twitch...";
            await _emoteResolver.LoadTwitchThirdPartyEmotesAsync(Settings.TwitchUsername);
            tasks.Add(_twitchService.ConnectAsync(Settings.TwitchUsername));
        }

        if (!string.IsNullOrWhiteSpace(Settings.KickUsername))
        {
            StatusText = "Connecting to Kick...";
            tasks.Add(_kickService.ConnectAsync(Settings.KickUsername));
        }

        if (tasks.Count == 0)
        {
            StatusText = "Enter at least one username";
            return;
        }

        try
        {
            await Task.WhenAll(tasks);
            IsConnected = true;
            StatusText = "Connected";
        }
        catch (Exception ex)
        {
            StatusText = $"Connection error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _twitchService.DisconnectAsync();
        await _kickService.DisconnectAsync();
        IsConnected = false;
        StatusText = "Disconnected";
    }

    [RelayCommand]
    private void ToggleBorders()
    {
        IsBorderVisible = !IsBorderVisible;
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
    }

    public void UpdateNotificationSound()
    {
        _soundService.SetSound(Settings.NotificationSound);
    }

    public void SaveSettings()
    {
        Settings.Save();
    }
}
