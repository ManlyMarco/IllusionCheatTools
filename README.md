# Koikatu! Trainer and Debugging Tools
Trainer for Koikatu! and Koikatsu Party. It allows control over player, girls, and most other aspects of the game. It is possible to view objects in current scene and inspect all of their variables and components. Inspector allows modifying values of the components in real time, while the browser allows disabling/enabling and destroying objects.

![Preview](https://user-images.githubusercontent.com/39247311/55248769-c359a380-524a-11e9-86cb-2a3fdb48abe8.PNG)

### How to use
- This is a BepInEx plugin. It requires at least BepInEx v4.
- Download the latest release from the [Releases](https://github.com/ManlyMarco/KoikatuCheatTools/releases) page.
- Install latest version of [RuntimeUnityEditor](https://github.com/ManlyMarco/RuntimeUnityEditor/releases/tag/v1.9).
- To install extract the .dll files in the BepInEx directory inside your game directory.
- To turn on press the Pause key (you can change it in plugin settings, search for "cheat").
- It's recommeded to turn on the system console to see if there are any issues with your game. This can be done from plugin settings, search for "system console". Also turn on "Unity message logging", then restart the game.

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
