using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TrackPostExtUpdator;

internal static class ShiftDetector
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_SHIFT = 0x10;

    // Polls the physical SHIFT key state and returns true if it is held continuously for 'holdMilliseconds'.
    // overallTimeoutMilliseconds limits how long the method will wait before returning false.
    internal static async Task<bool> CheckShiftHeld(
        int timeoutMs = 1200,
        int holdMs = 1000,
        int pollIntervalMs = 16
    )
    {
        if (holdMs <= 0)
            return false;

        var sw = Stopwatch.StartNew();
        long holdStart = -1;

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            bool isDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

            if (isDown)
            {
                if (holdStart < 0)
                {
                    // SHIFT pressed — mark the start time
                    holdStart = sw.ElapsedMilliseconds;
                }
                else
                {
                    // check continuous hold duration
                    var heldFor = sw.ElapsedMilliseconds - holdStart;
                    if (heldFor >= holdMs)
                    {
                        return true;
                    }
                }
            }
            else
            {
                // SHIFT released — reset
                holdStart = -1;
            }

            await Task.Delay(pollIntervalMs).ConfigureAwait(false);
        }

        return false;
    }
}
