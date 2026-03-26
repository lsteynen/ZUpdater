# Updater

<img src="./Resources/download.ico" alt="Updater logo" title="Updater" align="right" height="50" />

A program to help an application to update itself (for Windows).

The Updater is launched by the main application when it wants to check for updates.
The application passes its current version, along with any options.
The Updater reaches out to the update-source location and checks whether a newer version is available. If no update is available it can report that or stay silent.
If an update is available, the user is prompted to install it.
When the user confirms to install the new version, the Updater downloads the installer to a temporary directory, closes the running application, starts the installer, and then exits.
Configuration is handled through an XML file.

## Command line arguments
Updater behaviour can be controlled using a few command line arguments.
Usually the main application only passes its version number and optionally the -n flag. All additional configuration is loaded from Updater.xml.

|Option|Description|
|:----|:----|
|**-i**|Dumps various diagnostic and informational output|
|**-v 1.2.14**|The current application version, formats "1.2", "1.2.3" and "1.2.3.4" are supported|
|**-e**|Enables error reporting (e.g., when the update server cannot be reached)|
|**-n**| Inform user that no update is available. Silent otherwise|

## Updater.xml
The Updater.xml file defines the configuration used by the Updater.
Below is an overview of the  supported XML elements.

|Element|Description|
|:----|:----|
|**&lt;caption&gt;**| The title displayed in the message box title bar|
|**&lt;name&gt;**| The name of the application.
|**&lt;processname&gt;**| The name of the process that should be terminated before updating. Example: "MySuperApp".
|**&lt;version&gt;**| The current application version number, if not passed on command line.
|**&lt;infourl&gt;**| URL pointing to the update information file. For example:<br>file:\//Z:/Applications/MySuperApp/Updater/Updaterinfo.xml<br>http:\//server/directory/MySuperApp/Updaterinfo.xml<br>ftp:\//server/directory/MySuperApp/Updaterinfo.xml|
|**&lt;showinfo&gt;**| Show an error message if the update process fails|
|**&lt;showerrors&gt;**| Show error messages (e.g., when the server cannot be reached). If disabled, failures remain silent|
|**&lt;shownoupdate>&gt;**| Display a notification when no update is available|

The new version installer should be next to the Updater.xml file.

