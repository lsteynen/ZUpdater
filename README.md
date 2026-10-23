# ZUpdater

<img src="./Resources/Download.ico" alt="ZUpdater logo" title="ZUpdater" align="right" width="75" />

A program that helps an application update itself. For Windows.

The main application launches ZUpdater when it wants to check for updates, passing its current version number (or the version is set in `ZUpdater.xml`).

ZUpdater then:

1. Checks all configured update sources. File shares and the Github latest release API are supported. A source that is unavailable is skipped, and the error is written to the log panel (if shown). If no source can be reached, ZUpdater shows an error message.
2. Selects the highest version found. If it is newer than the application current version, the user is asked whether to download and install it. Otherwise the user is told that no update is available (or nothing is shown if ZUpdater runs hidden).
3. When the user confirms, downloads the installer to the `Downloads` folder, closes the running application, starts the installer, and exits.

Configuration is handled through an XML file and a few command line arguments.

## Command line arguments
ZUpdater behaviour can be controlled using a few command line arguments.
Usually the main application only passes its version number and optionally the -h flag. All additional configuration is loaded from `ZUpdater.xml`.

| Argument | Description |
|:----|:----|
| `-v 1.2.14` | The current application version number. Formats "1.2", "1.2.3" and "1.2.3.4" are supported. "1.2" is the same as "1.2.0.0". |
| `-h` | Run hidden. ZUpdater only shows a dialog if a new version was found or in case of severe error. |
| `-l` | Shows a log panel if the window is visible.|

Example of a hidden update check. Nothing is shown unless an update is available (or a severe error occurs), in which case the user is asked whether to download and install it:


`ZUpdater.exe -v 1.2.14 -h`

Example of a visible update check. A ZUpdater window shows status messages, including any errors. If an update is available, the user is asked whether to download and install it. If not, the user is told the application is up to date:

`ZUpdater.exe -v 1.2.14`

Example of a visible update check with log panel: 

`ZUpdater.exe -v 1.2.14 -l`

## ZUpdater.xml configuration
ZUpdater.xml tells ZUpdater which application to update and where to look for new versions.

ZUpdater.xml must be in the same folder as ZUpdater.exe.

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
    <location>https://api.github.com/repos/OWNER/REPO/releases/latest</location>
    <assetpattern>(?i).+\.msi$</assetpattern>
  </source>
</updater>
```

### Elements

| Element | Required | Description |
|:---|:---|:---|
| `<updater>` | Required | Root element. |
| `<caption>` | Required | String. Text shown to the user in the ZUpdater window title. |
| `<name>` | Required | String. Name of the application being updated. Shown in ZUpdater text.|
| `<processname>` | Required | String. Process name of the running application, without `.exe`. Used to close the running application before installing the new version. |
| `<version>` | Optional | String. The current application version number. Formats "1.2", "1.2.3" and "1.2.3.4" are supported. Used if application version number is not passed on the command line.|
| `<hidden>` | Optional | `true`/`false`. ZUpdater runs hidden if this element is `true` or the `-h` command line option is used. ZUpdater only shows a dialog if a new version was found or in case of severe error. |
| `<showlog>`| Optional | `true`/`false`. If the ZUpdater window is visible a log panel is shown if this element is `true` or the `-l` command line option is used. |
| `<source>` | Required, 1 or more | A location to look for an update. See below. |

### `<source>`

Each `<source>` has a `type` attribute and some child elements.

| Attribute | Required | Description |
|:---|:---|:---|
| `type` | Required | Kind of source: `fileshare` or `githubrelease`. |

### `type = "fileshare"`

```xml
<source type="fileshare">
  <location>\\Fileserver\Sources\MySuperApp\UpdaterInfo.xml</location>
</source>
```
| Element | Required | Description |
|:---|:---|:---|
| `<location>` | Required | Is the full path to an `UpdaterInfo.xml` file, on a network share (UNC path) or a local folder. See structure below.|

#### UpdaterInfo.xml
The `UpdaterInfo.xml` file describes the available version and installer file name or path:

```xml
<updater>
  <version>1.2.3.1501</version>
  <file>Setup MySuperApp.msi</file>
</updater>
```

| Element | Required | Description |
|:---|:---|:---|
| `<updater>` | Required | Root element.|
| `<version>` | Required | Newly available version, format "1.2", "1.2.3" or "1.2.3.4". The version of the application that the installer in the `file` element refers to.|
| `<file>` | Required | Installer file name, relative path or absolute path. Can be `.exe` or `.msi`. A bare file name or relative path is relative to the folder containing `UpdaterInfo.xml`. |

### `type = "githubrelease"`

```
<source type="githubrelease">
    <location>https://api.github.com/repos/OWNER/REPO/releases/latest</location>
    <assetpattern>(?i).+\.msi$</assetpattern>
  </source>
```
| Element | Required | Description |
|:---|:---|:---|
| `<location>` | Required | URL of the Github API of the latest release of a repository.|
| `<assetpattern>` | Required | A (.NET flavor) regex to match the correct asset file name in case the release has multiple assets attached. <br> Can be `.exe` or `.msi`. It is a regex so do not use a glob like`*.msi`. <br> For example: `(?i).+\.msi$` <br> `(?i)` means case insensitive <br> filename that has at least 1 character <br> filename ends with `.msi`. <br> The first asset that matches the regex is used. |

