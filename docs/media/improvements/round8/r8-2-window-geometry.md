# Tools/wmtest.sh window: where KWin puts the game's window (private KWin, virtual screen)

## Before R8-2 (Wayland, 1600x900 screen)

started frame 0,0 1600x900 client 0,0 1600x900 screen 1600x900 fullScreen true maximized 0 decorated true
  fits on the screen
step2 frame 0,0 1600x928 client 0,28 1600x900 screen 1600x900 fullScreen false maximized 0 decorated true
  DOESN'T FIT on the screen
step3 frame 0,0 1600x900 client 0,0 1600x900 screen 1600x900 fullScreen true maximized 0 decorated true
  fits on the screen
step4 frame 0,0 1600x928 client 0,28 1600x900 screen 1600x900 fullScreen false maximized 0 decorated true
  DOESN'T FIT on the screen

## After R8-2 (wl)

wmtest: window, wayland, screen 1600x900, load average: 36.95, 27.80, 18.68
WAYLAND_DISPLAY=ah-wmtest-1074139 DISPLAY=
started frame 0,0 1600x900 client 0,0 1600x900 screen 1600x900 fullScreen true maximized 0 decorated true
  fits on the screen
step2 frame 0,0 1600x928 client 0,28 1600x900 screen 1600x900 fullScreen false maximized 0 decorated true
  DOESN'T FIT on the screen
step3 frame 0,0 1600x900 client 0,0 1600x900 screen 1600x900 fullScreen true maximized 0 decorated true
  fits on the screen
step4 frame 0,0 1600x928 client 0,28 1600x900 screen 1600x900 fullScreen false maximized 0 decorated true
  DOESN'T FIT on the screen

## After R8-2 (x11)

wmtest: window, x11, screen 1600x900, load average: 25.94, 28.75, 20.84
WAYLAND_DISPLAY=ah-wmtest-1090591 DISPLAY=:2
started frame 0,0 1600x900 client 0,0 1600x900 screen 1600x900 fullScreen true maximized 3 decorated true
  fits on the screen
step2 frame 0,0 1280x748 client 0,28 1280x720 screen 1600x900 fullScreen false maximized 0 decorated true
  fits on the screen
step3 frame 0,0 1600x900 client 0,0 1600x900 screen 1600x900 fullScreen true maximized 0 decorated true
  fits on the screen
step4 frame 0,0 1280x748 client 0,28 1280x720 screen 1600x900 fullScreen false maximized 0 decorated true
  fits on the screen
PASS: fullscreen again renders at the screen's resolution

## After R8-2 (wl-1080)

wmtest: window, wayland, screen 1920x1080, load average: 23.63, 27.73, 20.83
WAYLAND_DISPLAY=ah-wmtest-1094522 DISPLAY=
started frame 0,0 1920x1080 client 0,0 1920x1080 screen 1920x1080 fullScreen true maximized 0 decorated true
  fits on the screen
step2 frame 0,0 1536x892 client 0,28 1536x864 screen 1920x1080 fullScreen false maximized 0 decorated true
  fits on the screen
step3 frame 0,0 1920x1080 client 0,0 1920x1080 screen 1920x1080 fullScreen true maximized 0 decorated true
  fits on the screen
step4 frame 0,0 1536x892 client 0,28 1536x864 screen 1920x1080 fullScreen false maximized 0 decorated true
  fits on the screen
PASS: fullscreen again renders at the screen's resolution
