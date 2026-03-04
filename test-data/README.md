# Test Data

This folder is for testing the Revit Upgrader application.

## Folder Structure

```
test-data/
├── sample-2024-files/    # Place your Revit 2024 .rvt files here
├── sample-2023-files/    # Place your Revit 2023 .rvt files here
├── sample-2025-files/    # Place your Revit 2025 .rvt files here
└── ...                   # Add more folders for different versions
```

## Usage

1. Copy your Revit files into the appropriate version folder
2. Run the Revit Upgrader application
3. Browse to one of these folders (e.g., `sample-2024-files`)
4. Select source and target versions
5. Click "Discover Files" to scan
6. Click "Start Upgrade" to begin the upgrade process

## Notes

- This folder is excluded from git (see `.gitignore`)
- You can create additional subfolders for different test scenarios
- **Always keep backups of your original files!**
- The upgrader will overwrite files in place by default

## Test Scenarios

You can organize your test files by scenario:

```
test-data/
├── sample-2024-files/
│   ├── basic/              # Simple files with no links
│   ├── with-links/         # Files with linked models
│   ├── workshared/         # Workshared files
│   └── complex/            # Complex files with families, etc.
```

This allows you to test different upgrade scenarios separately.
