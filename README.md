# Stream Chat Overlay

A lightweight, transparent chat overlay for **Twitch** and **Kick** — designed for single-monitor streamers who want to see chat without alt-tabbing.

Built with WPF/.NET 9. No browser sources, no Electron, no bloat.

## Features

- **Twitch + Kick chat** in a single overlay window
- **Transparent, always-on-top** — sits over your game with no background
- **Native emotes** — Twitch, Kick, BTTV, FrankerFaceZ, and 7TV (including animated GIFs)
- **Notification sounds** — Chirp, Bubble, Chime, Drop, Ping with volume control and preview
- **Resizable and draggable** — position and size the overlay however you want
- **System tray** — minimize to tray, toggle borders, reset position from the context menu
- **Font scaling** — 10pt to 72pt with emotes that scale proportionally
- **Dark-themed settings** — clean settings panel, no ugly Windows-default dialogs
- **Platform badges** — Twitch and Kick logos next to each message
- **Text shadow** — readable on any game background
- **No login required** — connects anonymously to public chat (read-only)

## Comparison

| Feature | Stream Chat Overlay | Transparent Twitch Chat Overlay |
|---|---|---|
| Twitch chat | Yes | Yes |
| Kick chat | Yes | No |
| Native emotes | Yes | Via browser |
| BTTV / FFZ / 7TV | Yes | Via browser |
| Animated emotes | Yes (GIF) | Via browser |
| Notification sounds | Yes (5 tones + volume) | No |
| Framework | WPF native | WebView2 / browser |
| Memory usage | ~30-50 MB | ~150-300 MB |
| Login required | No | No |

## Installation

### Download (Recommended)

1. **[Download the latest release](https://github.com/uruskan/StreamChatOverlay/releases/latest)** — grab the `.zip` file
2. Extract the zip to any folder
3. Run `StreamChatOverlay.exe`

> Requires [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0/runtime) — download the **"Run desktop apps"** version if you don't have it installed.

### Build from Source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
git clone https://github.com/uruskan/StreamChatOverlay.git
cd StreamChatOverlay
dotnet publish src/StreamChatOverlay/StreamChatOverlay.csproj -c Release -o publish
```

Run `publish/StreamChatOverlay.exe`.

## Setup

### Twitch

1. Open Settings (gear icon in title bar, or right-click tray icon)
2. Enter your **Twitch username**
3. Click **Connect**

That's it — no OAuth, no tokens. It connects anonymously to public chat via IRC.

### Kick

1. Enter your **Kick username** in Settings
2. Enter your **Kick Chatroom ID** (see below)
3. Click **Connect**

#### Finding your Kick Chatroom ID

Your chatroom ID is a permanent number tied to your Kick account (it never changes).

1. Go to `https://kick.com/api/v2/channels/YOUR_USERNAME` in your browser
2. In the JSON response, find `"chatroom": { "id": 12345678 }` — that number is your chatroom ID
3. Paste it into the **Kick Chatroom ID** field in settings

If Cloudflare blocks the API page:
1. Go to `kick.com/YOUR_USERNAME`
2. Open DevTools (F12) > **Network** tab
3. Refresh the page, filter by `channels`
4. In the response JSON, find `chatroom.id`

> **Note:** The chatroom ID is different from the channel ID. Make sure you use the one inside the `"chatroom"` object, not the top-level `"id"`.

## Usage

- **Drag** the title bar to move the overlay
- **Resize** using the grip in the bottom-right corner
- **Hide borders** — click the `[ ]` button or use the tray menu to make it fully transparent
- **Show borders** — right-click the tray icon > Toggle Borders
- **Clear chat** — right-click tray icon > Clear Chat
- **Reset position** — right-click tray icon > Reset Window Position

## Tech Stack

- **.NET 9** / WPF
- **TwitchLib** — Twitch IRC (anonymous)
- **KickLib** — Kick Pusher WebSocket
- **WpfAnimatedGif** — animated emote rendering
- **CommunityToolkit.Mvvm** — MVVM framework
- **H.NotifyIcon** — system tray integration

## License

[MIT](LICENSE)
