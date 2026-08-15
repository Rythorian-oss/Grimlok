# Grimlok — WPF Security Camera Monitor

**Grimlok** is a high-performance .NET 8 WPF application for real-time security camera monitoring, motion detection, AI object recognition, and automated alert management. Built with modern MVVM architecture and clad in an HR Giger-inspired biomechanical dark theme, Grimlok provides low-latency visual analysis and automated security dispatch.

---

## 👁️ Biomechanical Giger Visual Theme

Grimlok features a custom dark-mode WPF design system inspired by H.R. Giger's biomechanical aesthetic:

* **Color Palette**: Deep obsidian (`#0D0D11`), dark slate carbon (`#1A1C23`), bone-white accents (`#D1D5DB`), metallic iron borders (`#374151`), and bio-luminescent green telemetry glows (`#10B981` / `#059669`).
* **Cybernetic UI**: Custom-styled control templates for buttons, sliders, text fields, data lists, and status indicators.
* **Telemetry Overlay**: Real-time motion graphs, frame-rate counters, and detection bounding boxes rendered with high-contrast tactical overlays.

---

## 🏗️ Architecture & MVVM Pattern

The application is structured around modern .NET WPF practices:

* **MVVM Framework**: Powered by `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
* **Dependency Injection**: Integrated via `Microsoft.Extensions.Hosting`, providing strong lifecycle management for services and view models.
* **Asynchronous Execution**: Thread-safe frame capture and background AI inference decoupling the camera feed from UI rendering.
* **Separation of Concerns**:
  * **Views**: XAML layouts and control templates.
  * **ViewModels**: Data bindings, commands, and UI state handling.
  * **Services**: Hardware capture (`Emgu.CV`), object detection (`YoloDotNet`), email notifications (`MailKit`/`MimeKit`), image processing (`SixLabors.ImageSharp`), and logging (`Serilog`).
  * **Models**: Domain entities for camera configurations, motion events, snapshots, and alert definitions.

---

## ⚡ Key Features

1. **Real-Time Video Capture**: Support for local webcams, video files, and RTSP IP camera streams using OpenCV (`Emgu.CV`).
2. **Motion Analysis Engine**: Dynamic grayscale frame differencing, contour detection, and area filtering.
3. **YOLOv8 AI Detection**: Integrated YOLOv8 inference via `YoloDotNet` for high-precision object classification (e.g., person detection).
4. **Snapshot Storage**: Automatic high-quality snapshot saving using `SixLabors.ImageSharp`.
5. **Email Alert Dispatch**: Asynchronous email alerts with embedded incident snapshots via `MailKit` and `MimeKit`.
6. **Structured Logging**: Diagnostic logging to console and rolling disk logs via `Serilog`.

---

## ⚙️ Configuration

Application settings are managed through `appsettings.json` or overridden via environment variables prefixed with `GRIMLOK__`.

```json
{
  "Camera": {
    "Source": "0",
    "Width": 1280,
    "Height": 720,
    "Fps": 30
  },
  "Motion": {
    "PixelThreshold": 30,
    "MinimumContourArea": 500,
    "AlertCooldownSeconds": 60,
    "SnapshotCooldownSeconds": 5
  },
  "ObjectDetection": {
    "Enabled": true,
    "ModelPath": "Models/yolov8n.onnx",
    "Confidence": 0.5,
    "HumanConfirmedOnly": true,
    "HumanLabels": ["person"]
  },
  "Alerts": {
    "EmailEnabled": true,
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "PasswordEnvironmentVariable": "GRIMLOK_ALERTS_PASSWORD"
  }
}
```

---

## 🛠️ Prerequisites & Building

### Requirements
* **OS**: Windows 10/11 (x64)
* **SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* **Model File**: Place `yolov8n.onnx` into `Models/` if YOLO object detection is enabled.

### Build & Run
From the solution root directory:

```bash
# Restore dependencies
dotnet restore Grimlok.sln

# Build the project
dotnet build Grimlok.sln -c Release

# Run the WPF application
dotnet run --project Grimlok/Grimlok.csproj
```
