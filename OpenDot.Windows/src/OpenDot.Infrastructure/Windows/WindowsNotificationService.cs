namespace OpenDot.Infrastructure.Windows;

public interface INotificationService
{
    void ShowNotification(string title, string body, string? dotId = null);
}

public class WindowsNotificationService : INotificationService
{
    public void ShowNotification(string title, string body, string? dotId = null)
    {
        try
        {
            // Windows 10 WinRT toast notification
            var toastXml = $"""
            <toast>
                <visual>
                    <binding template="ToastGeneric">
                        <text>{System.Security.SecurityElement.Escape(title)}</text>
                        <text>{System.Security.SecurityElement.Escape(body)}</text>
                    </binding>
                </visual>
            </toast>
            """;

            var xmlDoc = new global::Windows.Data.Xml.Dom.XmlDocument();
            xmlDoc.LoadXml(toastXml);
            var toast = new global::Windows.UI.Notifications.ToastNotification(xmlDoc);
            global::Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("OpenDot.Windows").Show(toast);
        }
        catch
        {
            // Fallback for unpackaged development modes where AUMID is not registered
            Console.WriteLine($"[Notification] {title}: {body}");
        }
    }
}
