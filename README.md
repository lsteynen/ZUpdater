# ZUpdater

<img src="./Resources/Download.ico" alt="ZUpdater logo" title="ZUpdater" align="right" width="100" />

A program to help an application to update itself. For Windows.

ZUpdater is launched by the main application when it wants to check for updates.
The application passes its current version number.\
ZUpdater reaches out to the update-source locations and checks if a newer version is available. It supports fileshares and the Github latest release API. It selects the highest version it can find.\
If an update is available, the user is prompted to download and install it.\
When the user confirms to install the new version, the ZUpdater downloads the installer to the Downloads directory, closes the running application, starts the installer, and then exits.\
Configuration is handled through an XML file and a few command line options.

## Command line arguments
ZUpdater behaviour can be controlled using a few command line arguments.
Usually the main application only passes its version number and optionally the -h flag. All additional configuration is loaded from ZUpdater.xml.

| Argument | Description |
|:----|:----|
| `-v 1.2.14` | Required. The current application version, formats "1.2", "1.2.3" and "1.2.3.4" are supported. |
| `-h` | ZUpdater runs hidden. It only show a dialog f a new version was found or in case of severe error. |
| `-l` | Shows a log panel if the window is visible .|

## ZUpdater.xml configuration
`ZUpdater.xml` tells ZUpdater which application to update and where to look for new versions.

### Example

```xml
<?xml version="1.0" encoding="utf-8"?>
<updater>
  <caption>MySuperApp update</caption>
  <name>MySuperApp</name>
  <processname>MySuperApp</processname>
  <source type="fileshare">
    <location>\\Fileserver\Sources\MySuperApp\UpdaterInfo.xml</location>
  </source>
  <source type="githubrelease">
    <location>https://api.github.com/repos/<owner>/<repo>/releases/latest</location>
  </source>
</updater>
```

### Elements

| Element | Required | Description |
|:---|:---|:---|
| `<updater>` | Yes | Root element. |
| `<caption>` | Yes | Text shown to the user in the window title. |
| `<name>` | Yes | Name of the application being updated. |
| `<processname>` | Yes | Process name of the running application, without `.exe`. ZUpdater uses it to detect and close the application before installing the new version. |
| `<version>` | No | The current application version number, if not passed on command line.
| `<hidden>` | No | Runs hidden. It only show a dialog f a new version was found or in case of severe error. |
| `<showlog>`| No | Shows a log panel if the windows is visible. |
| `<source>` | Yes, one or more | A location to look for an update. See below. |

### `<source>`

Each `<source>` has a `type` attribute and a `<location>` child element.

| Attribute / element | Description |
|---|---|
| `type` | Kind of source: `fileshare` or `githubrelease`. |
| `<location>` | Where to find the update information. Its meaning depends on `type`. |

### `type="fileshare"`

`<location>` is the full path to an `UpdaterInfo.xml` file, on a network share (UNC path) or a local folder:

```xml
<source type="fileshare">
  <location>\\Fileserver\Sources\MySuperApp\UpdaterInfo.xml</location>
</source>
```

That info file describes the available version and installer:

```xml
<updater>
  <version>1.2.3.1501</version>
  <file>Setup MySuperApp.msi</file>
</updater>
```

| Element | Description |
|---|---|
| `<version>` | Latest available version, format '1.2' or '1.2.3' or '1.2.3.4'. |
| `<file>` | Installer file name, or its full path. A bare file name is resolved relative to the folder containing `UpdaterInfo.xml`. |

### `type="githubrelease"`

`<location>` is the GitHub REST API URL of the latest release of a repository:

```
https://api.github.com/repos/<owner>/<repo>/releases/latest
```

```xml
<source type="githubrelease">
  <location>https://api.github.com/repos/theowner/therepo/releases/latest</location>
</source>
```
