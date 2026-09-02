using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace KeyGlance.Helper;

public sealed class ForegroundGuard(AutomationElement targetWindow)
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    public void EnsureTargetIsForeground()
    {
        var targetHandle = new nint(targetWindow.Current.NativeWindowHandle);
        if (targetHandle == 0 || GetForegroundWindow() != targetHandle)
            throw new OperationCanceledException("The MockTax window stopped being the foreground window.");
    }
}
