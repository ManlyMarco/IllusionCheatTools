## Trainer for games by Illusion
Trainer for Koikatu, Koikatsu Party and AI-Syoujyo / AI-Shoujo. It allows control over player, heroines, and most other aspects of the game. It also integrates with RuntimeUnityEditor to provide useful debugging tools.

![Preview](https://user-images.githubusercontent.com/39247311/55248769-c359a380-524a-11e9-86cb-2a3fdb48abe8.PNG)

### How to use
1. Get at least [BepInEx v5.0](https://github.com/BepInEx/BepInEx/releases) and [RuntimeUnityEditor v2.0](https://github.com/ManlyMarco/RuntimeUnityEditor/releases).
2. Download the latest release archive for your game from the [releases](https://github.com/ManlyMarco/IllusionCheatTools/releases) page.
3. Extract the release in the game directory, the .dll file should end up inside BepInEx\plugins.
4. To turn on press the Pause key or F12 key depending on the release and game (you can change it in plugin settings, search for "cheat").
5. It's recommeded to turn on the system console to see if there are any issues with your game. This can be done from plugin settings, search for "system console". Also turn on "Unity message logging", then restart the game.

### FAQ
Q: The plugin is making the game run slow.
- A: This is either the result of using OnGUI in which case there is little that can be done, or because of an incompatibility with the game that is causing many exceptions to be thrown. In the second case it might be possible to fix the slowdown, start a new issue then.

Q: It doesn't work
- A: Update your game, BepInEx and plugins to their latest versions. You can use the [HF Patch](https://github.com/ManlyMarco/KK-HF_Patch
) to do it easily.

Q: I found a bug / can't make it work
- A: Ask on the [Koikatsu! discord server](https://discord.gg/zS5vJYS) in the #help channel.

-------
You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco
