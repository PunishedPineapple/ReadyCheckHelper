# ReadyCheckHelper

## Purpose

This is a plugin for [XIVLauncher/Dalamud](https://github.com/goatcorp/FFXIVQuickLauncher) that extends the game's ready check.

## Usage
This plugin works automatically, writing out the names of those that were not ready in the chat following a ready check so that you don't have to catch the few seconds the game gives you to open the ready check window before it disappears.  The text emitted will look something like:

![Screenshot](Images/image1.png)

You can configure the number of names shown before you get an "and \<x\> others" in the settings window for the plugin.

It can also display the ready check flags on the party/alliance lists in realtime during a ready check like this:

![Screenshot](Images/image2.png)

This feature can be turned on or off in the plugin config.  Please note that until you respond to a ready check, the icons on your party/alliance list may not be updated.  This is a limitation of how the game handles ready checks, and I don't think that I can do anything about it.

## Contributing
If you would like to contribute translations for currently unsupported languages, please request access on the Crowdin page for this plugin (same name as this github repo).

## License
Code and executable are covered under the [MIT License](../LICENSE).  Final Fantasy XIV (and any associated data used by this plugin) is owned by and copyright Square Enix.

Plugin icon modified from https://thenounproject.com/icon/people-1350210/ by b farias.
