# Revit Upgrader

A robust standalone application for batch upgrading Revit files from 2024 to 2026, with intelligent popup handling and comprehensive logging.

## Overview

This tool automates the tedious process of upgrading multiple Revit files while intelligently handling the various popup dialogs that Revit displays during upgrades. The application launches Revit, opens files one-by-one, and uses UI Automation (FlaUI) to detect and handle popups automatically.

## Architecture

### Single Application Design

```
┌─────────────────────────────────────┐
│  RevitUpgrader.App (WPF App)        │
│  - User Interface                   │
│  - File Discovery                   │
│  - Revit Automation                 │
│  - Popup Detection (FlaUI)          │
│  - Screenshot Capture               │
│  - Logging & Progress Tracking      │
└─────────────────────────────────────┘
              │
              ▼
     Launches & Controls
              │
┌─────────────▼───────────────────────┐
│      Autodesk Revit 2026            │
│  - Opens files (auto-upgrades)      │
│  - Displays popups                  │
│  - Saves upgraded files             │
└─────────────────────────────────────┘
```

### Project Structure

```
RevitUpgrader/
├── README.md                          # This file
├── RevitUpgrader.sln                  # Visual Studio Solution
│
├── RevitUpgrader.Core/                # Shared models and utilities
│   └── Models/
│       ├── UpgradeJob.cs             # Job configuration
│       ├── FileStatus.cs             # File processing status
│       ├── PopupHandler.cs           # Popup handler configuration
│       └── LogEntry.cs               # Structured log entry
│
└── RevitUpgrader.App/                 # Main WPF Application ⭐
    ├── UI/
    │   ├── MainWindow.xaml/cs        # Main UI
    │   └── Components/               # Reusable UI components
    ├── Services/
    │   ├── FileDiscovery.cs          # Recursive .rvt file finder
    │   ├── RevitAutomation.cs        # Launch Revit, open/close files
    │   └── UpgradeOrchestrator.cs    # Main workflow coordinator
    ├── PopupHandling/
    │   ├── PopupDetector.cs          # FlaUI-based popup detection
    │   ├── PopupMatcher.cs           # Match detected popup to handlers
    │   ├── PopupActionExecutor.cs    # Execute actions on popups
    │   └── ScreenshotCapture.cs      # Screenshot utility
    ├── Logging/
    │   └── UpgradeLogger.cs          # Structured logging
    └── Config/
        └── popup-handlers.json       # Popup handler configuration
```

## Key Features

### 1. Intelligent Popup Handling

- **Configurable Handlers**: Define popup handlers in JSON with flexible matching criteria
- **Multi-Property Matching**: Match popups by window title, message content, button configuration
- **Action Sequences**: Execute a sequence of actions (click, wait, type) for each popup
- **Screenshot Capture**: Automatically capture screenshots of all popups (known and unknown)
- **Unknown Popup Management**: Safely skip files with unrecognized popups for manual review

### 2. Robust File Processing

- **Recursive Discovery**: Find all `.rvt` files in a directory tree
- **Backup Filtering**: Automatically exclude backup files (e.g., `.0001.rvt`)
- **One-at-a-time Processing**: Process files sequentially for reliable error tracking
- **In-place Upgrade**: Overwrites original files with upgraded versions

### 3. Comprehensive Logging

- **Structured Logs**: JSON-formatted logs with timestamps, durations, and status
- **File-level Tracking**: Track each file's upgrade status, time taken, popups encountered
- **Popup Details**: Log all popup properties, matched handlers, and actions taken
- **Screenshots**: Store screenshots of popups alongside logs
- **Configurable Verbosity**: Support for different log levels (info, verbose, debug)

### 4. Reliable IPC Communication

- **Named Pipes**: Fast, reliable inter-process communication
- **Status Updates**: Real-time status updates from Revit to Monitor
- **Error Handling**: Graceful handling of communication failures
- **Command/Response Pattern**: Clear request/response flow

## Workflow

1. **User launches RevitUpgrader.exe**
   - Select root folder containing Revit files
   - Configure settings (log level, popup handler config path)

2. **Application discovers files**
   - Recursively scan for `.rvt` files
   - Filter out backup files (e.g., `.0001.rvt`, `.0002.rvt`)
   - Display list of files to be upgraded
   - User can exclude specific files if needed

3. **Application launches Revit 2026**
   - Start Revit process
   - Wait for Revit to be ready

4. **For each file:**
   ```
   a. Application: Opens file in Revit (via journal file/command line)
   b. Revit: Automatically starts upgrade process
   c. Application: Watches for popups (continuous monitoring)
   d. Application: Detects popup, matches to handler
   e. Application: Executes action sequence (click OK, etc.)
   f. Application: Captures screenshot of popup
   g. Application: If unknown popup → skip file, log details
   h. Application: Sends save & close command to Revit
   i. Application: Logs result (success/skipped/failed with duration)
   ```

5. **Application displays summary**
   - Show upgrade results
   - Display skipped files with reasons
   - Provide links to log files and screenshots

## Popup Handler Configuration

Popup handlers are defined in `popup-handlers.json`:

```json
{
  "popupHandlers": [
    {
      "id": "upgrade-confirmation",
      "enabled": true,
      "priority": 1,
      "matchers": {
        "windowTitle": "Autodesk Revit 2026",
        "messageContains": "upgrade",
        "buttons": ["OK", "Cancel"]
      },
      "actions": [
        {
          "type": "click",
          "target": "OK"
        },
        {
          "type": "wait",
          "milliseconds": 500
        }
      ],
      "captureScreenshot": true
    },
    {
      "id": "missing-links",
      "enabled": true,
      "priority": 2,
      "matchers": {
        "messageContains": "linked files",
        "windowTitleRegex": ".*Warning.*"
      },
      "actions": [
        {
          "type": "click",
          "target": "Close"
        }
      ],
      "captureScreenshot": true
    }
  ],
  "unknownPopupBehavior": {
    "action": "skip",
    "captureScreenshot": true,
    "logDetails": true
  }
}
```

### Matcher Properties

- **windowTitle**: Exact window title match
- **windowTitleRegex**: Regex pattern for window title
- **messageContains**: Substring in dialog message
- **messageRegex**: Regex pattern for message
- **buttons**: Array of button text expected
- **controlId**: AutomationId of specific control

### Action Types

- **click**: Click a button (by text or AutomationId)
- **wait**: Wait for specified milliseconds
- **type**: Send keystrokes to a control
- **log**: Log a custom message
- **screenshot**: Take additional screenshot

### Priority

Handlers are matched in priority order (lower number = higher priority). The first matching handler is used.

## Installation

### Prerequisites

- Windows 10/11
- .NET Framework 4.8 or .NET 6+
- Autodesk Revit 2026
- Visual Studio 2022 (for building)

### Building

1. Open `RevitUpgrader.sln` in Visual Studio 2022
2. Restore NuGet packages (automatic):
   - FlaUI.UIA3
   - Serilog
   - Newtonsoft.Json
3. Build the solution (Release mode recommended)

### Deployment

1. **Standalone Application**:
   - Copy `RevitUpgrader.exe` and dependencies to a folder
   - Copy `popup-handlers.json` to the same folder
   - Ensure Revit 2026 is installed on the machine
   - No Revit addin installation required!

## Usage

### Basic Usage

1. Launch `RevitUpgrader.exe`
2. Click "Select Folder" and choose the root folder containing `.rvt` files
3. Review the list of discovered files
4. (Optional) Exclude specific files from upgrade
5. (Optional) Adjust settings (log level, Revit path)
6. Click "Start Upgrade"
7. Watch progress in real-time
8. Review log and screenshots after completion

### Advanced Configuration

- **Custom Popup Handlers**: Edit `popup-handlers.json` to add new handlers
- **Log Levels**: Set via UI dropdown (Info, Verbose, Debug)
- **Screenshot Directory**: Configure output directory for screenshots
- **IPC Settings**: Configure pipe name and timeout (advanced)

## Logging

Logs are written to:
- `Logs/upgrade-{timestamp}.json` - Structured JSON log
- `Logs/upgrade-{timestamp}.txt` - Human-readable text log
- `Screenshots/{file-name}/{popup-id}-{timestamp}.png` - Screenshot per popup

### Log Entry Format

```json
{
  "timestamp": "2026-03-04T14:30:00Z",
  "level": "Info",
  "eventType": "FileUpgradeComplete",
  "filePath": "C:\\Projects\\Building.rvt",
  "status": "Success",
  "duration": "00:00:45",
  "popupsEncountered": 2,
  "popupsHandled": 2,
  "details": {
    "fileSize": "45MB",
    "popups": [
      {
        "id": "upgrade-confirmation",
        "action": "clicked OK",
        "screenshot": "Screenshots/Building/upgrade-confirmation-20260304143000.png"
      }
    ]
  }
}
```

## Troubleshooting

### Application Can't Launch Revit

- Ensure Revit 2026 is installed
- Check Revit installation path in settings
- Verify Revit license is valid
- Try manually launching Revit first to ensure it works

### Popups Not Detected

- Verify FlaUI can see Revit windows (use UIA Spy tool)
- Check popup-handlers.json syntax
- Increase detection polling interval
- Check log for detection errors

### Files Being Skipped

- Review log for "Unknown Popup" entries
- Check screenshots folder for popup images
- Add new handler to popup-handlers.json for that popup
- Re-run upgrade on skipped files

### Performance Issues

- Reduce polling frequency (default: 200ms)
- Disable verbose logging
- Close other applications to free resources

## Technical Details

### FlaUI (UI Automation)

The application uses FlaUI to interact with Revit windows. FlaUI wraps the Windows UI Automation API and provides:
- Cross-framework support (WPF, Win32, WinForms)
- Reliable control identification
- Synchronous and asynchronous operations
- Screenshot capabilities

### Revit Automation Methods

The application can control Revit using:
1. **Journal Files** (Recommended): Script files that Revit executes
2. **Command Line**: Launch Revit with file path arguments
3. **UI Automation**: Send keyboard/mouse commands via FlaUI

### Thread Safety

- UI thread: WPF interface
- Background thread: Popup detection and monitoring
- All file operations are sequential (one file at a time)
- Thread-safe logging using Serilog

## Contributing

To add new popup handlers:
1. Encounter the popup during testing
2. Check the screenshot and log for popup properties
3. Add a new handler entry to `popup-handlers.json`
4. Test the handler on a sample file
5. Share the handler configuration with the team

## Future Enhancements

- [ ] Support for Revit 2025, 2027, etc.
- [ ] Machine learning for popup classification
- [ ] Parallel file processing (multiple Revit instances)
- [ ] Cloud-based logging and reporting
- [ ] Rollback capability (keep backup copies)
- [ ] Email notifications on completion
- [ ] Integration with CI/CD pipelines

## License

Internal use only. Autodesk Revit is a registered trademark of Autodesk, Inc.

## Contact

For questions or issues, contact the development team.
