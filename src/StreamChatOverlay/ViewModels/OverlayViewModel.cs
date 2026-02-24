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
    private bool _hasError;

    public OverlayViewModel()
    {
        _settings = AppSettings.Load();
        _soundService.SetSound(_settings.NotificationSound);

        _twitchService.OnMessageReceived += HandleMessage;
        _twitchService.OnError += err =>
            Application.Current.Dispatcher.InvokeAsync(() => SetError($"Twitch error: {err}"));
        _twitchService.OnConnected += () =>
            Application.Current.Dispatcher.InvokeAsync(() => SetStatus("Twitch connected"));
        _twitchService.OnDisconnected += () =>
            Application.Current.Dispatcher.InvokeAsync(() => SetStatus("Twitch disconnected"));

        _kickService.OnMessageReceived += HandleMessage;
        _kickService.OnError += err =>
            Application.Current.Dispatcher.InvokeAsync(() => SetError($"Kick error: {err}"));
        _kickService.OnConnected += () =>
            Application.Current.Dispatcher.InvokeAsync(() => SetStatus("Kick connected"));
        _kickService.OnDisconnected += () =>
            Application.Current.Dispatcher.InvokeAsync(() => SetStatus("Kick disconnected"));
    }

    // Errors stick — non-error status won't overwrite an error
    private void SetError(string msg)
    {
        _hasError = true;
        StatusText = msg;
    }

    private void SetStatus(string msg)
    {
        if (!_hasError)
            StatusText = msg;
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
        _hasError = false;
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
            var manualId = string.IsNullOrWhiteSpace(Settings.KickChatroomId) ? null : Settings.KickChatroomId.Trim();
            tasks.Add(_kickService.ConnectAsync(Settings.KickUsername, manualId));
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
            if (!_hasError)
                StatusText = "Connected";
        }
        catch (Exception ex)
        {
            SetError($"Connection error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _twitchService.DisconnectAsync();
        await _kickService.DisconnectAsync();
        IsConnected = false;
        _hasError = false;
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

    public void PreviewSound(string soundName, double volume)
    {
        _soundService.PlayPreview(soundName, volume);
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
