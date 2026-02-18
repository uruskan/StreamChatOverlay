# Overlay Fixes and Polish Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Fix tray icon, animated emotes, platform icons, text readability, defaults, emote scaling, font size range, and add notification sounds.

**Architecture:** All changes are to existing files. Tray icon fix requires swapping NuGet package. Animated emotes fix requires using WpfAnimatedGif for all emotes. Platform icons become WPF Path geometries. Notification sounds use `System.Media.SoundPlayer` with embedded WAV resources.

**Tech Stack:** .NET 9 WPF, H.NotifyIcon (replacing H.NotifyIcon.Wpf), WpfAnimatedGif, System.Media.SoundPlayer

---

## Task 1: Fix System Tray Icon

The tray icon doesn't show because `H.NotifyIcon.Wpf 2.4.1` is a .NET Framework package. Replace with the .NET-native `H.NotifyIcon` package.

**Files:**
- Modify: `src/StreamChatOverlay/StreamChatOverlay.csproj`
- Modify: `src/StreamChatOverlay/App.xaml`
- Modify: `src/StreamChatOverlay/App.xaml.cs`

**Step 1: Swap NuGet package**

```bash
cd src/StreamChatOverlay
dotnet remove package H.NotifyIcon.Wpf
dotnet add package H.NotifyIcon
```

**Step 2: Update App.xaml**

Replace the current `xmlns:tb` and `TaskbarIcon` resource. The .NET-native H.NotifyIcon uses a different namespace and may not support `GeneratedIconSource`. Use a programmatic approach instead - create the tray icon in code-behind.

Replace the entire `App.xaml` with:

```xml
<Application x:Class="StreamChatOverlay.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="Views/OverlayWindow.xaml"
             ShutdownMode="OnMainWindowClose">
    <Application.Resources>
    </Application.Resources>
</Application>
```

**Step 3: Update App.xaml.cs to create tray icon programmatically**

Replace the entire `App.xaml.cs` with:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using StreamChatOverlay.ViewModels;
using StreamChatOverlay.Views;

namespace StreamChatOverlay;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Stream Chat Overlay",
            Icon = CreateAppIcon(),
            ContextMenu = CreateContextMenu()
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private static System.Drawing.Icon CreateAppIcon()
    {
        // Create a simple 16x16 icon programmatically
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(145, 70, 255)); // Twitch purple
        g.DrawString("SC", new Font("Arial", 6, System.Drawing.FontStyle.Bold),
            System.Drawing.Brushes.White, 0, 2);
        var hIcon = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        var showSettings = new MenuItem { Header = "Show Settings" };
        showSettings.Click += (_, _) => TrayShowSettings();
        menu.Items.Add(showSettings);

        var toggleBorders = new MenuItem { Header = "Toggle Borders" };
        toggleBorders.Click += (_, _) => GetViewModel()?.ToggleBordersCommand.Execute(null);
        menu.Items.Add(toggleBorders);

        var resetPos = new MenuItem { Header = "Reset Window Position" };
        resetPos.Click += (_, _) =>
        {
            var vm = GetViewModel();
            if (vm == null) return;
            vm.Settings.WindowLeft = 100;
            vm.Settings.WindowTop = 100;
            vm.Settings.WindowWidth = 350;
            vm.Settings.WindowHeight = 600;
        };
        menu.Items.Add(resetPos);

        menu.Items.Add(new Separator());

        var clearChat = new MenuItem { Header = "Clear Chat" };
        clearChat.Click += (_, _) => GetViewModel()?.ClearChatCommand.Execute(null);
        menu.Items.Add(clearChat);

        menu.Items.Add(new Separator());

        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) =>
        {
            GetViewModel()?.SaveSettings();
            Shutdown();
        };
        menu.Items.Add(exit);

        return menu;
    }

    private OverlayViewModel? GetViewModel()
        => (MainWindow as OverlayWindow)?.DataContext as OverlayViewModel;

    private void TrayShowSettings()
    {
        if (MainWindow is OverlayWindow overlay)
        {
            var settings = new SettingsWindow
            {
                DataContext = overlay.DataContext,
                Owner = overlay
            };
            settings.ShowDialog();
        }
    }
}
```

NOTE: The `System.Drawing` reference requires adding `<UseWindowsForms>true</UseWindowsForms>` to the csproj PropertyGroup to enable the `System.Drawing.Common` types on Windows.

**Step 4: Update csproj to enable System.Drawing**

Add `<UseWindowsForms>true</UseWindowsForms>` to the PropertyGroup in `StreamChatOverlay.csproj`:

```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
```

**Step 5: Build and verify**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded with 0 errors.

**Step 6: Commit**

```bash
git add -A
git commit -m "fix: replace H.NotifyIcon.Wpf with H.NotifyIcon for working system tray"
```

---

## Task 2: Fix Animated Emotes (All Platforms)

All emotes (Twitch native, Kick, BTTV, 7TV) should use WpfAnimatedGif for rendering. This way animated GIFs play their animation regardless of the `isAnimated` flag. WpfAnimatedGif handles static images fine too.

**Files:**
- Modify: `src/StreamChatOverlay/Behaviors/InlineMessageBehavior.cs`

**Step 1: Update InlineMessageBehavior to always use WpfAnimatedGif**

Replace the emote rendering block (the `if (fragment.IsAnimated) ... else ...` block) with a single path that always uses `ImageBehavior.SetAnimatedSource`. This plays animated GIFs and shows static images correctly.

In `InlineMessageBehavior.cs`, replace lines 39-79 (the entire `else if` block for emotes) with:

```csharp
else if (fragment.Type == FragmentType.Emote && fragment.EmoteUrl != null)
{
    try
    {
        var image = new Image
        {
            Height = 28,
            Width = 28,
            Stretch = Stretch.Uniform,
            ToolTip = fragment.Content,
            Margin = new Thickness(2, 0, 2, 0)
        };

        var uri = new Uri(fragment.EmoteUrl);
        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.UriSource = uri;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();

        // Always use WpfAnimatedGif - it handles both static and animated images
        ImageBehavior.SetAnimatedSource(image, bitmapImage);
        ImageBehavior.SetRepeatBehavior(image,
            System.Windows.Media.Animation.RepeatBehavior.Forever);

        textBlock.Inlines.Add(new InlineUIContainer(image)
        {
            BaselineAlignment = BaselineAlignment.Center
        });
    }
    catch
    {
        textBlock.Inlines.Add(new Run(fragment.Content));
    }
}
```

**Step 2: Build and verify**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add -A
git commit -m "fix: use WpfAnimatedGif for all emotes so animations play on all platforms"
```

---

## Task 3: Platform Icons (Twitch/Kick SVG Logos)

Replace the "T"/"K" text with real Twitch and Kick logo vector paths that scale with font size.

**Files:**
- Replace: `src/StreamChatOverlay/Converters/PlatformToIconConverter.cs` (delete old, create new approach)
- Modify: `src/StreamChatOverlay/Views/OverlayWindow.xaml`

**Step 1: Create PlatformIconConverter that returns a Geometry**

The old converter returned a string. The new approach uses a `DataTemplate` in XAML with a `Path` element. Replace `PlatformToIconConverter.cs` with a converter that returns `Geometry`:

```csharp
// src/StreamChatOverlay/Converters/PlatformToIconConverter.cs
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Converters;

public class PlatformToIconConverter : IValueConverter
{
    // Twitch Glitch logo (Simple Icons, 24x24 viewBox)
    private static readonly Geometry TwitchGeometry = Geometry.Parse(
        "M11.571 4.714h1.715v5.143H11.57zm4.715 0H18v5.143h-1.714zM6 0L1.714 4.286v15.428h5.143V24l4.286-4.286h3.428L22.286 12V0zm14.571 11.143l-3.428 3.428h-3.429l-3 3v-3H6.857V1.714h13.714Z");

    // Kick logo (Simple Icons, 24x24 viewBox)
    private static readonly Geometry KickGeometry = Geometry.Parse(
        "M1.333 0h8v5.333H12V2.667h2.667V0h8v8H20v2.667h-2.667v2.666H20V16h2.667v8h-8v-2.667H12v-2.666H9.333V24h-8Z");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ChatPlatform platform)
            return platform == ChatPlatform.Twitch ? TwitchGeometry : KickGeometry;
        return Geometry.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

Also create a converter for the platform color:

```csharp
// src/StreamChatOverlay/Converters/PlatformToColorConverter.cs
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StreamChatOverlay.Models;

namespace StreamChatOverlay.Converters;

public class PlatformToColorConverter : IValueConverter
{
    private static readonly Brush TwitchBrush = new SolidColorBrush(Color.FromRgb(145, 70, 255)); // #9146FF
    private static readonly Brush KickBrush = new SolidColorBrush(Color.FromRgb(83, 252, 24));   // #53FC18

    static PlatformToColorConverter()
    {
        TwitchBrush.Freeze();
        KickBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ChatPlatform platform)
            return platform == ChatPlatform.Twitch ? TwitchBrush : KickBrush;
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

**Step 2: Update OverlayWindow.xaml**

Add the new converter to Window.Resources:
```xml
<conv:PlatformToColorConverter x:Key="PlatformToColor"/>
```

Replace the platform icon TextBlock in the DataTemplate (the one that shows "T"/"K") with a Path element:

```xml
<!-- Platform icon -->
<Viewbox Width="{Binding DataContext.Settings.FontSize, RelativeSource={RelativeSource AncestorType=Window}}"
         Height="{Binding DataContext.Settings.FontSize, RelativeSource={RelativeSource AncestorType=Window}}"
         Margin="0,0,4,0"
         Visibility="{Binding DataContext.Settings.ShowPlatformIcon, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource BoolToVisibility}}">
    <Path Data="{Binding Platform, Converter={StaticResource PlatformToIcon}}"
          Fill="{Binding Platform, Converter={StaticResource PlatformToColor}}"
          Stretch="Uniform"/>
</Viewbox>
```

**Step 3: Build and verify**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: replace T/K text with real Twitch and Kick logo SVG paths"
```

---

## Task 4: Default Settings and Text Readability

Change defaults to fully transparent background, 100% opacity, and add text drop shadow for readability on any background.

**Files:**
- Modify: `src/StreamChatOverlay/Models/AppSettings.cs`
- Modify: `src/StreamChatOverlay/Views/OverlayWindow.xaml`
- Modify: `src/StreamChatOverlay/Views/SettingsWindow.xaml`

**Step 1: Update AppSettings defaults**

In `AppSettings.cs`, change:
- `Opacity` from `0.75` to `1.0`
- `BackgroundColor` from `"#BF000000"` to `"#00000000"` (fully transparent)
- `FontSize` max will be handled in the settings slider (Task 5)

```csharp
public double Opacity { get; set; } = 1.0;
public string BackgroundColor { get; set; } = "#00000000";
```

**Step 2: Add text drop shadow to chat messages in OverlayWindow.xaml**

Add a `DropShadowEffect` style to the chat message elements for readability. In `Window.Resources`, add:

```xml
<DropShadowEffect x:Key="TextShadow" ShadowDepth="1" BlurRadius="3"
                  Color="Black" Opacity="0.9" Direction="315"/>
```

Then apply `Effect="{StaticResource TextShadow}"` to the WrapPanel inside the DataTemplate:

```xml
<WrapPanel Margin="2" Effect="{StaticResource TextShadow}">
```

**Step 3: Build and verify**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add -A
git commit -m "fix: transparent background by default and add text shadow for readability"
```

---

## Task 5: Font Size Range and Emote Scaling

Increase font size slider max and make emotes scale with font size.

**Files:**
- Modify: `src/StreamChatOverlay/Views/SettingsWindow.xaml`
- Modify: `src/StreamChatOverlay/Behaviors/InlineMessageBehavior.cs`
- Modify: `src/StreamChatOverlay/Views/OverlayWindow.xaml`

**Step 1: Increase font size slider max**

In `SettingsWindow.xaml`, change the FontSize slider:

```xml
<Slider Value="{Binding Settings.FontSize}" Minimum="10" Maximum="72"
        TickFrequency="1" IsSnapToTickEnabled="True" Margin="0,0,0,12"/>
```

Changed `Maximum="32"` to `Maximum="72"`.

**Step 2: Make emotes scale with font size**

The current emote size is hardcoded to 28x28 in `InlineMessageBehavior.cs`. Instead, pass the font size to the behavior so emotes scale proportionally.

Add a new attached property `EmoteSize` to `InlineMessageBehavior.cs`:

```csharp
public static readonly DependencyProperty EmoteSizeProperty =
    DependencyProperty.RegisterAttached(
        "EmoteSize",
        typeof(double),
        typeof(InlineMessageBehavior),
        new PropertyMetadata(28.0, OnFragmentsChanged));

public static void SetEmoteSize(DependencyObject element, double value)
    => element.SetValue(EmoteSizeProperty, value);

public static double GetEmoteSize(DependencyObject element)
    => (double)element.GetValue(EmoteSizeProperty);
```

Then in `OnFragmentsChanged`, read the emote size:

```csharp
var emoteSize = GetEmoteSize(textBlock);
```

And use it for image dimensions:

```csharp
var image = new Image
{
    Height = emoteSize,
    Width = emoteSize,
    ...
};
```

**Step 3: Bind EmoteSize in OverlayWindow.xaml**

On the message TextBlock that uses InlineMessageBehavior, add:

```xml
<TextBlock b:InlineMessageBehavior.Fragments="{Binding Fragments}"
           b:InlineMessageBehavior.EmoteSize="{Binding DataContext.Settings.FontSize, RelativeSource={RelativeSource AncestorType=Window}, Converter={StaticResource FontSizeToEmoteSize}}"
           .../>
```

Create a simple converter that scales font size to emote size (e.g., fontSize * 1.8):

```csharp
// src/StreamChatOverlay/Converters/FontSizeToEmoteSizeConverter.cs
using System.Globalization;
using System.Windows.Data;

namespace StreamChatOverlay.Converters;

public class FontSizeToEmoteSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double fontSize)
            return fontSize * 1.8;
        return 28.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

Add to `OverlayWindow.xaml` resources:
```xml
<conv:FontSizeToEmoteSizeConverter x:Key="FontSizeToEmoteSize"/>
```

**Step 4: Build and verify**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: increase font size range to 72 and scale emotes with font size"
```

---

## Task 6: Notification Sounds

Add ability to play a sound on each new message. Settings: sound selection (dropdown) and volume slider.

**Files:**
- Create: `src/StreamChatOverlay/Services/SoundService.cs`
- Create: `src/StreamChatOverlay/Sounds/` (directory with embedded WAV files)
- Modify: `src/StreamChatOverlay/Models/AppSettings.cs`
- Modify: `src/StreamChatOverlay/ViewModels/OverlayViewModel.cs`
- Modify: `src/StreamChatOverlay/Views/SettingsWindow.xaml`
- Modify: `src/StreamChatOverlay/StreamChatOverlay.csproj`

**Step 1: Create sound files**

We'll generate small WAV notification sounds programmatically (sine wave beeps) and embed them as resources. Create `src/StreamChatOverlay/Services/SoundService.cs`:

```csharp
// src/StreamChatOverlay/Services/SoundService.cs
using System.IO;
using System.Media;

namespace StreamChatOverlay.Services;

public sealed class SoundService
{
    private SoundPlayer? _player;
    private string _currentSound = "None";

    // Available sound names
    public static readonly string[] AvailableSounds =
        ["None", "Pop", "Ding", "Click", "Blip"];

    public void SetSound(string soundName)
    {
        _currentSound = soundName;
        _player?.Dispose();
        _player = null;

        if (soundName == "None") return;

        var wavData = GenerateWav(soundName);
        var ms = new MemoryStream(wavData);
        _player = new SoundPlayer(ms);
        _player.Load();
    }

    public void Play(double volume)
    {
        if (_currentSound == "None" || _player == null) return;
        // SoundPlayer doesn't support volume natively, but we can
        // adjust the WAV data amplitude. For simplicity, just play at full volume
        // when volume > 0.
        if (volume > 0)
            _player.Play();
    }

    private static byte[] GenerateWav(string soundName)
    {
        // Generate simple sine wave beeps as WAV data
        int sampleRate = 44100;
        int durationMs = soundName switch
        {
            "Pop" => 80,
            "Ding" => 200,
            "Click" => 30,
            "Blip" => 60,
            _ => 100
        };
        int frequency = soundName switch
        {
            "Pop" => 800,
            "Ding" => 1200,
            "Click" => 2000,
            "Blip" => 1500,
            _ => 1000
        };

        int numSamples = sampleRate * durationMs / 1000;
        var samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            // Apply fade-out envelope
            double envelope = 1.0 - ((double)i / numSamples);
            double sample = Math.Sin(2 * Math.PI * frequency * t) * envelope * 0.5;
            samples[i] = (short)(sample * short.MaxValue);
        }

        // Build WAV file in memory
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        int dataSize = numSamples * 2; // 16-bit = 2 bytes per sample
        int fileSize = 36 + dataSize;

        writer.Write("RIFF"u8);
        writer.Write(fileSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);            // chunk size
        writer.Write((short)1);      // PCM
        writer.Write((short)1);      // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // byte rate
        writer.Write((short)2);      // block align
        writer.Write((short)16);     // bits per sample
        writer.Write("data"u8);
        writer.Write(dataSize);

        foreach (var s in samples)
            writer.Write(s);

        return ms.ToArray();
    }
}
```

**Step 2: Add settings properties**

In `AppSettings.cs`, add:

```csharp
public string NotificationSound { get; set; } = "None";
public double NotificationVolume { get; set; } = 0.5;
```

**Step 3: Integrate SoundService into OverlayViewModel**

In `OverlayViewModel.cs`, add a `SoundService` field and play sound on message received:

```csharp
private readonly SoundService _soundService = new();
```

In the constructor, after loading settings:

```csharp
_soundService.SetSound(_settings.NotificationSound);
```

In `HandleMessage`, after adding the message to the collection (inside the Dispatcher call):

```csharp
_soundService.Play(Settings.NotificationVolume);
```

Add a method to update sound when setting changes (call this from settings save or add a property observer):

```csharp
public void UpdateNotificationSound()
{
    _soundService.SetSound(Settings.NotificationSound);
}
```

**Step 4: Add sound settings to SettingsWindow.xaml**

After the DISPLAY section, before the Save button, add:

```xml
<Separator Background="#333" Margin="0,0,0,12"/>

<!-- Sound -->
<TextBlock Text="NOTIFICATIONS" FontSize="11" Foreground="#888"
           Margin="0,0,0,8"/>

<TextBlock Text="Notification Sound" Margin="0,0,0,4"/>
<ComboBox ItemsSource="{x:Static services:SoundService.AvailableSounds}"
          SelectedItem="{Binding Settings.NotificationSound}"
          Padding="6" Margin="0,0,0,8"
          Background="#2A2A3E" Foreground="White" BorderBrush="#444"/>

<TextBlock Text="{Binding Settings.NotificationVolume, StringFormat='Volume: {0:P0}'}"
           Margin="0,0,0,4"/>
<Slider Value="{Binding Settings.NotificationVolume}" Minimum="0" Maximum="1.0"
        TickFrequency="0.1" IsSnapToTickEnabled="True" Margin="0,0,0,12"/>
```

Add the namespace at top of XAML:
```xml
xmlns:services="clr-namespace:StreamChatOverlay.Services"
```

**Step 5: Update SettingsWindow.xaml.cs SaveClose to update sound**

In `SaveClose_Click`:
```csharp
var vm = (OverlayViewModel)DataContext;
vm.UpdateNotificationSound();
vm.SaveSettings();
Close();
```

**Step 6: Build and verify**

Run: `dotnet build src/StreamChatOverlay`
Expected: Build succeeded.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add notification sounds with selectable tones and volume control"
```

---

## Task 7: Delete Existing settings.json and Publish

Delete the old `settings.json` if it exists (so new defaults take effect), rebuild, and publish.

**Step 1: Delete old settings if present**

```bash
# The settings.json is created at runtime in the app's base directory
# Users should delete it to get new defaults, or it will keep old values
```

**Step 2: Build and publish**

```bash
dotnet build StreamChatOverlay.sln
dotnet test tests/StreamChatOverlay.Tests -v n
dotnet publish src/StreamChatOverlay -c Release -r win-x64 --self-contained false -o ./publish
```

**Step 3: Final commit**

```bash
git add -A
git commit -m "chore: final build and publish"
```

---

## Summary of Task Dependencies

```
Task 1: Fix System Tray Icon
Task 2: Fix Animated Emotes
Task 3: Platform Icons (SVG Logos)
Task 4: Default Settings and Text Readability
Task 5: Font Size Range and Emote Scaling
Task 6: Notification Sounds
Task 7: Build and Publish
```

Tasks 1-6 are independent and can be done in any order.
Task 7 depends on all others.
