# What is this?
This thing let's Discord see your Steam games that are running with Proton.
EDIT: now also works for non proton linux games too, because discord also shows the wrong short process name for them, instead of the full name of the game.

**Also works with Flatpak Steam**

## What's funny?
- While making this I realized something.
- Discord normally supports Proton games for Non-Flatpak Steam but its buggy and doesn't work until you go to settings and view `Activity Settings`.
- Then it triggers something and it magically shows the game.

# Build And Usage
## Build (Skip this part if you are just gonna download the executables)
- You need to have `dotnet` sdk for `.NET6.0` (You only need this, if you wanna build it yourself)
- Then run `build.sh`.
- It's gonna create 2 executable files in `./dist`.
### If you don't wanna build
Download executables: https://github.com/DeepDoge/discord-steam-proton-rpc/releases/download/release-5/dist.zip
## Usage
- Just run the the `discord-steam-proton-rpc` in `dist` directory.
- - Or add it to your startup application.

**NOTE: rpc-trigger and discord-steam-proton-rpc should always be in the same directory together.**

**This is all you need to do. For basic usage.**