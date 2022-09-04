# RunOnDisplayWakeup

[ledoge/novideo_srgb](https://github.com/ledoge/novideo_srgb) is a fantastic application to clamp wide gamut displays to sRGB. Unfortunately whenever the display goes into standby mode, [the clamp is lost](https://github.com/ledoge/novideo_srgb/issues/46) and manual reapply is needed within novideo_srgb. As a quick workaround, I created this little .NET Core background service which does the following:

1. Polls NVAPI every 5 seconds to check if the primary display is not active.
2. When the display becomes active, it will gracefully shutdown novideo_srgb.exe using "taskkill".
3. Restarts novideo_srgb.exe with the "-minimize" argument.

Gets the job done, but would be nice for this behavior to exist in novideo_srgb itself.
