# Consol
Consol is a work-in-progress mod for the Unity game Valheim. It aims to replicate the functionality of the existing mod, SkToolbox,
but with a simpler approach to creating commands.

Consol intends to be a replacement for the default in-game console by offering a new set of commands, better visuals, history, scrolling, and more.

## Features
Below is a list of features planned for the mod. Checked items are currently implemented, while unchecked ones are still in-progress.

- [x] Advanced command processing.
- [x] Debug logging.
- [ ] Basic utility/administration commands.
- [ ] New console GUI.
- [ ] Console history/scrolling.
- [ ] Command history.

## Building
If you'd prefer to build Consol yourself rather than use the available binary, download the repository and open the .sln file in Visual Studio.  
Valheim relies on the .NET Framework version 4.7.2, so you may need to install that.

Before you can build, this mod relies on the following library (.dll) files:
- BepInEx
- 0Harmony
- assembly_valheim
- UnityEngine

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
3. Click "Build Consol".

Alternatively, instead of steps 2-3, just press CTRL+B.

You now have a `Consol.dll` file to use as you please, located under `bin/Release/Consol.dll`. Install like you would any other BepInEx plugin.