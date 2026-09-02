using System.Windows.Automation;

namespace KeyGlance.Helper;

public sealed class TaxFieldWriter(ForegroundGuard foreground)
{
    public string WriteAndRead(AutomationElement window, string automationId, string expected)
    {
        foreground.EnsureTargetIsForeground();
        var field = WindowFinder.FindExactField(window, automationId);
        field.SetFocus();

        if (!field.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObject))
            throw new InvalidOperationException($"Field does not expose a writable value: {automationId}");

        ((ValuePattern)patternObject).SetValue(expected);
        foreground.EnsureTargetIsForeground();
        return ((ValuePattern)field.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
    }
}
