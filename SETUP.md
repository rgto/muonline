# Setup & Usage Guide

How to get the MU Online hybrid client running from a fresh clone.

> **Educational / research project.** No proprietary game data is included in this
> repository — you must supply your own client data (see step 2).

---

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| **.NET 10 SDK** | https://dotnet.microsoft.com/download/dotnet/10.0 |
| **Git** | to clone the repo |
| A **Season 6 server** | e.g. [OpenMU](https://github.com/MUnique/OpenMU) via Docker (see step 4) |

Platform-specific:

- **Windows** — nothing extra (DirectX 11 or OpenGL backend).
- **Linux** — `libsdl2`, `libopenal`, Mesa/GL drivers.
- **Android** — the .NET Android workload: `dotnet workload install android`
  (plus the Android SDK). Runs on emulator or BlueStacks.
- **macOS / iOS** — Xcode + the respective .NET workload.

---

## 2. Game data (`Data.zip`)

The client needs the MU client asset pack (models, textures, maps, interface).
This is **not** shipped in the repo.

### Quick path — prebuilt pack

1. Download the ready-to-use pack:
   **https://drive.google.com/file/d/1c_quFvBT6YfoR5rfCiV4bK6nppFQRIr3/view?usp=sharing**
2. Save it as **`Data.zip`** inside the repo's **`data/`** folder:

   ```
   <repo>/data/Data.zip
   ```

That's it — the client extracts/serves from there on first run.

### Which assets are correct

The client is tuned for the **official Western MU Online, Season 21 or newer**
asset set. If you'd rather assemble the pack yourself instead of using the Drive
link above, pull the assets from any of these and zip the `Data` folder:

- The **official Western client** (Webzen / global).
- Any **IGCN-based client**, e.g. **https://play.muonline.eu/downloads**.
- **RealMU** client.

Point is: use S21+ Western assets. Older/regional packs may render some UI or
models incorrectly.

> The network protocol stays **Season 6** (that's what OpenMU speaks) — only the
> *art/data* is S21+. The two are independent.

---

## 3. Server settings

Edit **`Client.Main/appsettings.json`** and set the address of your server:

```json
{
  "MuOnlineSettings": {
    "ConnectServerHost": "127.0.0.1",
    "ConnectServerPort": 44405,
    "ProtocolVersion": "Season6"
  }
}
```

On **Android/BlueStacks** you don't have to edit the file — the first screen in
the app ("Connection Settings") lets you type the Host and Port directly.

---

## 4. Run a server (OpenMU)

Any Season 6 compatible server works. The simplest is OpenMU in Docker:

```bash
curl -o docker-compose.yml https://raw.githubusercontent.com/MUnique/OpenMU/master/deploy/all-in-one/docker-compose.yml
docker compose up -d
```

It listens on `localhost:44405`, matching the default client config.

---

## 5. Build & run

Restore tooling once:

```bash
dotnet tool restore
```

Then launch the head for your platform:

```bash
# Windows — DirectX 11 (recommended)
dotnet run --project ./MuWinDX/MuWinDX.csproj -f net10.0-windows -c Debug -p:MonoGameFramework=MonoGame.Framework.WindowsDX

# Windows — OpenGL (compatibility)
dotnet run --project ./MuWinGL/MuWinGL.csproj -f net10.0-windows -c Debug -p:MonoGameFramework=MonoGame.Framework.DesktopGL

# Linux
dotnet run --project ./MuLinux/MuLinux.csproj -f net10.0 -c Debug

# macOS
dotnet run --project ./MuMac/MuMac.csproj -f net10.0 -c Debug
```

### Android (emulator / BlueStacks)

```bash
# Build a signed APK
dotnet build ./MuAndroid/MuAndroid.csproj -c Debug -f net10.0-android

# Install it
adb install -r ./MuAndroid/bin/Debug/net10.0-android/MuMono.MuMono-Signed.apk
```

The APK is **universal** (arm64-v8a + x86_64), so it installs on any BlueStacks
build. Just drag the `.apk` onto the BlueStacks window, or use `adb install`.

On first launch the app shows the **Connection Settings** screen — enter your
server Host/Port and tap OK.

---

## 6. First login

- Default test account is pre-filled (`test1` / `test1`) — change it to a real
  account on your server.
- In-game keys: **I** or **V** = inventory, **C** = character, **A** = mastery
  (master skill tree). On touch/mobile, use the icons in the top-right corner and
  the fold-out menu.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Black screen / missing models | `data/Data.zip` is missing or not the S21+ Western pack. |
| Can't connect | Check Host/Port in `appsettings.json` (or the Connection screen on Android) and that the server is up on `:44405`. |
| Android install fails mid-transfer | Emulator ran out of memory — restart the emulator and `adb install -r` again. |
| NuGet restore fails (`NU1301`) | Network/proxy issue — verify connectivity before touching code. |
