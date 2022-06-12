# Gungnir
Gungnir is a mod for the Unity game Valheim. It aims to replicate the functionality of the existing mod, SkToolbox,
but with a simpler approach to creating commands and a more straightforward code base.

Gungnir enhances the default in-game console by offering a new set of commands, better visuals, scrolling, and more.

## Features
Below is a list of features planned for the mod. Checked items are currently implemented, while unchecked ones are still in-progress.

- [x] Advanced command processing.
- [x] Debug logging.
- [x] Basic utility/administration commands.
- [x] New console theme.
- [x] Console history/scrolling.
- [ ] Autocomplete for each argument of a command (if applicable).

## Screenshots

Helpful new commands.  
![Overview](https://i.imgur.com/ODRz8Nb.png)
<br>
Succinct and visually pleasing command feedback.  
![Feedback](https://i.imgur.com/GnlrVpx.png)
<br>
Integration with autocomplete, and partial matching.  
![Autocomplete and partial matching](https://i.imgur.com/CSRKkEA.png)
<br>
Item/prefab searching.  
![Searching](https://i.imgur.com/qPryHeQ.png)

### Technical To-Do
For a more technical view of what's planned, refer to this list. These may or may not include end-user features, and is just a general list of things I'd like to
incorporate into the mod, or change about the implementation.

- [ ] Use GUI components rather than OnGUI drawing functions.
	- [ ] Selectable console text. Currently, GUILayout functions don't have a good way to accomplish this.
	- [ ] Patch `Console` class methods to redirect to new GUI elements (Print, updateSearch).
	- [ ] Custom auto-complete text handler.
- [x] Better error message handling. Utility functions should not be printing anything. Specific exceptions should be thrown, or an error context object should be given as an output parameter.
- [ ] API for other mods to register commands?

## Building from Source
If you'd prefer to build Gungnir yourself rather than use the available binary, download the repository and open the .sln file in Visual Studio.  
Valheim relies on the .NET Framework version 4.7.2, so you may need to install that.

Before you can build, this mod relies on the following library (.dll) files:
- BepInEx
- 0Harmony
- All unstripped DLLs provided by BepInEx for Valheim
- assembly_valheim
- assembly_utils

These files should not be distributed by third parties, so you will need to locate them yourself. The last two can be found in your Valheim game installation folder.

Once you have these files:
1. Place them near this project's .sln file.
2. In Visual Studio, right-click "References", then click "Add Reference".
3. Click "Browse".
4. Select all of the .dll files mentioned.
5. Click "Add".

Now the project should be ready to build!
1. Change the build configuration to "Release", if it isn't already.
2. Click "Build" from the top of Visual Studio.
3. Click "Build Gungnir".

Alternatively, instead of steps 2-3, just press CTRL+B.

You now have a `Gungnir.dll` file to use as you please, located under `bin/Release/Gungnir.dll`. Install like you would any other BepInEx plugin.