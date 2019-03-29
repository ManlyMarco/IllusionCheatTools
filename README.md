# Koikatu! Trainer and Debugging Tools
Trainer for Koikatsu with advanced debugging tools. It allows control over player, girls, and most other aspects of the game. It is possible to view objects in current scene and inspect all of their variables and components. Inspector allows modifying values of the components in real time, while the browser allows disabling/enabling and destroying objects.

![Preview](https://user-images.githubusercontent.com/39247311/55248769-c359a380-524a-11e9-86cb-2a3fdb48abe8.PNG)

### How to use
- This is a BepInEx plugin. It requires BepInEx v4 or later.
- Download the latest CheatTools.dll from the [Releases](https://github.com/ManlyMarco/KoikatuCheatTools/releases) page.
- To install place the .dll in the BepInEx directory inside your game directory.
- To turn on press the F12 key. You can open the browser and inspector from the main menu in the top right.

### FAQ
Q: The plugin is making the game run slow.
- A: Unfortunately this is the result of using OnGUI and there is little that can be done. Developer console plugin can lower the performance to unacceptable levels, use the separate console instead (Plugin settings > Show advanced > BepInEx > Show console, then restart the game).

Q: It doesn't work
- A: Update your game, BepInEx and plugins to their latest versions. You can use the [HF Patch](https://github.com/ManlyMarco/KK-HF_Patch
) to do it easily.

Q: I found a bug / can't make it work
- A: Ask on the [Koikatsu! discord server](https://discord.gg/zS5vJYS) in the #help channel.

-------
You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco
