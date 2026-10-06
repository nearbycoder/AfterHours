After Hours v{version} (Linux x86-64)
https://github.com/nearbycoder/AfterHours

Run:   ./AfterHours.sh
The launcher uses Unity's native Wayland backend in a Wayland session (the player can hang at
start-up under XWayland) and X11 otherwise. AH_X11=1 ./AfterHours.sh forces X11. You can also
start ./AfterHours.x86_64 directly, adding -force-wayland yourself if the window never appears.

Needs 64-bit Linux and an OpenGL 3.2+ GPU. Saves and settings go to
~/.config/unity3d/After Hours Team/After Hours/

Controls: WASD and mouse to move and look, left mouse to clean (hold to charge a throw),
right mouse to spray, E to interact, Q to drop, C to crouch, F for the UV torch (from Night 2),
Tab for the clipboard, Esc to pause. Gamepads work too.
