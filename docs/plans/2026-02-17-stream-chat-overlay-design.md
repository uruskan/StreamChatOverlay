# Stream Chat Overlay - Design Document

## Overview

A lightweight WPF desktop application that displays combined Twitch and Kick chat as a transparent, always-on-top overlay. Replaces TransparentTwitchChat with multi-platform support.

## Requirements

- Enter Twitch and Kick usernames (no API keys, no OAuth)
- Display combined chat as a transparent overlay on screen
- Always-on-top over borderless-fullscreen games (Rust, Dota, etc.)
- Inline emote rendering including animated GIFs (Twitch, BTTV, FFZ, 7TV, Kick)
- Badge display (mod, sub, VIP, etc.)
- Usable as an OBS Window Capture source
- Two modes: setup mode (with borders/controls) and overlay mode (borderless)
- Taskbar icon always visible with right-click context menu for control

## Technology

- .NET 8, WPF, MVVM (CommunityToolkit.Mvvm)
- TwitchLib.Client for Twitch IRC (anonymous via justinfan)
- System.Net.WebSockets for Kick Pusher API
- WpfAnimatedGif for animated emote support
- System.Text.Json for JSON parsing

## Architecture

### Core Components

| Component | Responsibility |
|-----------|---------------|
| SettingsWindow | Username inputs, appearance customization, connect/disconnect |
| OverlayWindow | Transparent, borderless, always-on-top chat display |
| TwitchChatService | Anonymous IRC connection via TwitchLib.Client |
| KickChatService | WebSocket connection to Kick Pusher API |
| EmoteResolver | Fetches and caches emote images (Twitch, BTTV, 7TV, FFZ, Kick) |
| ChatMessage | Unified message model from both platforms |

### Data Flow

1. User enters usernames in SettingsWindow, clicks Connect
2. TwitchChatService joins #username anonymously (justinfan12345)
3. KickChatService resolves chatroom ID via public API, subscribes via Pusher WebSocket
4. Both services emit ChatMessage objects on a shared ObservableCollection
5. OverlayWindow binds to collection via ItemsControl with custom DataTemplate
6. Each message renders: platform icon, colored username, message text with inline emotes

## Overlay Window Behavior

### Window Properties

- AllowsTransparency="True", WindowStyle="None", Topmost="True"
- Semi-transparent background (configurable opacity)
- Taskbar icon always visible

### Two Modes

| Mode | Behavior |
|------|----------|
| Setup mode | Visible border with drag handle, resize grips, settings button, close button |
| Overlay mode | Borderless - just floating chat text. Controlled via taskbar context menu |

### Taskbar Right-Click Context Menu

- Show/Hide Settings
- Reset Window Position & Size
- Toggle Borders (switch between setup/overlay mode)
- Disconnect
- Exit

### Chat Display

- Top-to-bottom direction (newest messages at bottom)
- Auto-scroll to newest message
- Max 200 message buffer (oldest removed)
- Optional message fade-out after configurable time

### Always-On-Top

- WPF Topmost=True works over borderless-fullscreen games
- Exclusive fullscreen requires user to run games in borderless windowed mode (standard for streaming)

### OBS Capture

- OBS Window Capture grabs the window directly
- Transparent background passes through in OBS

## Customization Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Twitch username | (empty) | Channel to join |
| Kick username | (empty) | Channel to join |
| Font size | 14px | Chat text size |
| Opacity | 75% | Window background opacity |
| Background color | Black | Background behind chat |
| Text color | White | Default message text color |
| Username colors | Per-platform | Twitch uses Twitch colors, Kick uses Kick colors |
| Show platform icon | On | Small T/K icon next to each message |
| Show badges | On | Mod, sub, VIP badges |
| Show emotes | On | Render emotes inline |
| Max messages | 200 | Buffer limit |
| Message fade | Off | Optional fade-out after X seconds |

Settings persisted to JSON file in app directory.

## Chat Connections (No Auth Required)

### Twitch

- Connect to irc.chat.twitch.tv via TwitchLib.Client
- Anonymous login as justinfan12345
- JOIN #channelname
- Emote images from public CDNs (Twitch, BTTV, FFZ, 7TV)

### Kick

- Resolve chatroom ID: GET kick.com/api/v2/channels/{username} (public, no auth)
- Connect to Pusher WebSocket: wss://ws-us2.pusher.com/app/{app_key}
- Subscribe to chatrooms.{id}.v2
- Emote images from Kick's public CDN

## NuGet Packages

- CommunityToolkit.Mvvm
- TwitchLib.Client
- WpfAnimatedGif
- (System.Text.Json and System.Net.WebSockets are built-in)
