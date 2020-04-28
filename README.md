# SamplePlugin
Simple example plugin for XivLauncher/Dalamud, that shows both a working plugin and an associated UI test project, to allow for building and tweaking the UI without having to run the game.

This is not designed to be the simplest possible example, but neither is it designed to cover everything you might want to do.

I'm mostly hoping this helps some people to build out their UIs without having to constantly jump in and out of game.


### Main Points
* Simple functional plugin
  * Slash command
  * Main UI
  * Settings UI
  * Image loading
  * Plugin json
* Simple, slightly-improved plugin configuration handling
* Basic ImGui testbed application project
  * Allows testing UI changes without needing to run the game
  * UI environment provided should match what is seen in game
  * Defaults to an invisible fullscreen overlay; can easily be changed to use an opaque window etc
  * Currently relies more on copy/paste of your UI code than fully generic objects (though this could be done)
* Project organization
  * Copies all necessary plugin files to the output directory
    * Does not copy dependencies that are provided by dalamud
    * Output directory can be zipped directly and have exactly what is required
  * Hides data files from visual studio to reduce clutter
    * Also allows having data files in different paths than VS would usually allow if done in the IDE directly
    
  
  The intention is less that any of this is used directly in other projects, and more to show how similar things can be done.
  
  The UIDev project could be used as-is, with just the UITest.cs file needing to be redone for your specific project UI.
  
  ### To Use
  You'll need to fixup the library dependencies (for both projects), to point at your local dalamud binary directory.
  
  This will either be a custom dalamud build, or `%APPDATA%\XivLauncher\addon\Hooks\` for the current live release.
  
  After that, clear out what you don't need in UITest.cs, and implement your own local UI under Draw()
  
