## CheatTools - Trainer for specific games running in Unity game engine
A trainer mostly for games by Illusion/Illgames (Koikatu, Koikatsu Party, AI-Shoujo, HoneySelect2, HoneyCome, Summer Vacation Scramble etc.). It allows control over player, heroines, and most other aspects of the game (supported features depend on the game). It also integrates with RuntimeUnityEditor to provide useful debugging tools.

![Preview](https://user-images.githubusercontent.com/39247311/55248769-c359a380-524a-11e9-86cb-2a3fdb48abe8.PNG)

### How to use
1. Install latest versions of [BepInEx v5.x](https://github.com/BepInEx/BepInEx/releases) and [RuntimeUnityEditor](https://github.com/ManlyMarco/RuntimeUnityEditor/releases). For HC and SVS you need the latest [nightly builds of BepInEx6](https://builds.bepinex.dev/projects/bepinex_be), [BepisPlugins](https://github.com/IllusionMods/BepisPlugins), and the IL2CPP version of RuntimeUnityEditor.
2. Download the latest release archive for your game from the [releases](https://github.com/ManlyMarco/IllusionCheatTools/releases) page.
3. Extract the release in the game directory, the .dll file should end up inside BepInEx\plugins.
4. To turn on press the Pause key or F12 key depending on the release and game (you can change it in plugin settings if you have the ConfigurationManager plugin, search for "runtime editor hotkey").
5. It's recommeded to turn on the system console to see if there are any issues with your game. This can be done from plugin settings, search for "system console". Also turn on "Unity message logging", then restart the game.

### FAQ
Q: The plugin is making the game run slow.
- A: This is either the result of using OnGUI in which case there is little that can be done, or because of an incompatibility with the game that is causing many exceptions to be thrown. In the second case it might be possible to fix the slowdown, start a new issue then.

Q: It doesn't work
- A: Update your game, BepInEx and plugins to their latest versions. You can use a HF Patch to do it easily if one is available for your game, check https://www.patreon.com/ManlyMarco. When updating the RuntimeUnityEditor, make sure that you don't have any old versions in BepInEx\Plugins or this plugin won't work!

Q: I found a bug / can't make it work
- A: Ask on the [Koikatsu! discord server](https://discord.gg/zS5vJYS) in the #help channel.

-------
You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco
