using System.Windows.Automation;

namespace KeyGlance.Helper;

public sealed class WindowFinder
{
    public AutomationElement FindExact(string client, int year)
    {
        var expectedTitle = $"MockTax - {client} {year}";
        var windows = AutomationElement.RootElement.FindAll(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));

        var matches = windows.Cast<AutomationElement>()
            .Where(window => string.Equals(window.Current.Name, expectedTitle, StringComparison.Ordinal))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"Exact MockTax window not found: {expectedTitle}"),
            _ => throw new InvalidOperationException($"More than one exact MockTax window found: {expectedTitle}")
        };
    }

    public static AutomationElement FindExactField(AutomationElement window, string automationId)
    {
        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        var fields = window.FindAll(TreeScope.Descendants, condition).Cast<AutomationElement>().ToList();
        return fields.Count switch
        {
            1 => fields[0],
            0 => throw new InvalidOperationException($"Field AutomationId not found: {automationId}"),
            _ => throw new InvalidOperationException($"Duplicate field AutomationId found: {automationId}")
        };
    }
}
