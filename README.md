# What is this?

This thing let's Discord see your Steam games that are running with Proton.<br/>
This is not really using Discord's Rich Rresence, but using Discord's Steam game detection. So they look like any other Steam game.<br/>
EDIT: now also works for non proton linux games too, because discord also shows the short process name for them, instead of the full name of the game.

**Also works with Flatpak Steam**

# Build And Usage

## Build (Skip this part if you are just gonna download the executables)
- You need to have `dotnet` sdk for `.NET6.0` (You only need this, if you wanna build it yourself)
- - Or you can use the VSCode DevContainer extension and open the project in Container.
- Then run `build.sh`.
- It's gonna create 2 executable files in `./dist`.

### If you don't wanna build

Download executables: https://github.com/DeepDoge/discord-steam-proton-rpc/releases/download/0.1.0/dist.zip

### If you were using an older version of this before `0.1.0`

[Read this](https://github.com/DeepDoge/discord-steam-proton-rpc/issues/4#issuecomment-1614823328)

## Usage

- Just run the the `discord-steam-proton-rpc` in `dist` directory.
- - Or add it to your startup application.

**NOTE: rpc-trigger and discord-steam-proton-rpc should always be in the same directory together.**

**This is all you need to do. For basic usage.**
