# Localization smoke check

Runs on Windows in an isolated STA process. It loads the actual WPF application
resources and lays out the real window at its minimum size, without calling
`Application.Run`, showing a desktop window, creating a tray icon, or reading and
writing the user's notes/settings. Any sample data uses a temporary directory.

```powershell
dotnet run --project tools/LocalizationSmoke/LocalizationSmoke.csproj -c Release
```

Optional `--screenshots <absolute-directory>` saves PNGs rendered directly from
the test's WPF visual tree; these are not screenshots of the user's desktop.
