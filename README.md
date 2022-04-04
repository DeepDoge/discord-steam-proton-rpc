# What is this?
This thing let's Discord see your Steam games that are running with Proton.

**Also works with Flatpak Steam**

## What's funny?
- While making this I realized something.
- Discord normally supports Proton games for Non-Flatpak Steam but its buggy and doesn't work until you go to settings and view `Activity Settings`.
- Then it triggers something and it magically shows the game.

# Build And Usage
## Build
- You need to have `dotnet` sdk for `.NET6.0`
- Then run `build.sh`.
- It's gonna create 2 executable files in `./dist`.
### If you don't wanna build
Download executables: https://github.com/DeepDoge/discord-steam-proton-rpc/releases/tag/release-2
## Usage
- Just run the the `discord-steam-proton-rpc` in `dist` directory.
- - Or add it to your startup application.

**NOTE: rpc-trigger and discord-steam-proton-rpc should always be in the same directory together.**

**This is all you need to do. For basic usage.**

### Additional (You can skip this part if you're using Flatpak Steam)
- Since Steam installs Proton inside `steamapps/common`, Discord also sees random Proton processes as Steam games. 
- And Discord can't always pick the right process to show on `Game Activity`.
- To cause less confusion for Discord you can move folders you wanna hide from Discord out of the `steamapps/common`. 
- And link them back inside the `steamapps/common`. So Discord ignores them while Steam can still use them. 
```bash
cd "/home/YOUR_USER/.steam/steam/steamapps"
mkdir "common_hidden"

mv -f "./common/YOUR_PROTON_FOLDER" "./common_hidden/YOUR_PROTON_FOLDER"
ln -s "./common_hidden/YOUR_PROTON_FOLDER" "./common/YOUR_PROTON_FOLDER"

mv -f "./common/SteamLinuxRuntime_soldier" "./common_hidden/SteamLinuxRuntime_soldier"
ln -s "./common_hidden/SteamLinuxRuntime_soldier" "./common/SteamLinuxRuntime_soldier"
```
