After Hours v{version} (macOS 12 or later, Apple Silicon and Intel)
https://github.com/nearbycoder/AfterHours

This build is UNSIGNED and has NOT been tested on a Mac. It was built on Linux, and nobody has
launched it on real Mac hardware yet. Reports are welcome as GitHub issues.

Because it isn't signed or notarised, macOS will refuse to open it the first time:
  1. Unzip it and move "After Hours.app" to Applications (or anywhere you like).
  2. Right-click (or Control-click) the app and choose Open, then Open again in the dialog.
     On recent macOS versions you may instead need System Settings → Privacy & Security →
     "Open Anyway".
  Or, in Terminal:   xattr -dr com.apple.quarantine "/Applications/After Hours.app"

Saves and settings go under ~/Library/Application Support/ (Unity names the folder after the
game or its bundle id, com.nearbycoder.afterhours; this hasn't been checked on a Mac).

Controls: WASD and mouse to move and look, left mouse to clean (hold to charge a throw),
right mouse to spray, E to interact, Q to drop, C to crouch, F for the UV torch (from Night 2),
Tab for the clipboard, Esc to pause. Gamepads work too.
