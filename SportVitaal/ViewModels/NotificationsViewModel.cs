using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the "Meldingen" page: the member's in-app notification feed (waitlist spots, expiring
/// membership, reservation and purchase confirmations). Opening a notification marks it read.
/// </summary>
public partial class NotificationsViewModel : ObservableObject
{
    private static readonly string[] Days = { "Zo", "Ma", "Di", "Wo", "Do", "Vr", "Za" };
    private static readonly string[] Months =
        { "jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec" };

    private readonly ApiClient _api;

    public NotificationsViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private IReadOnlyList<NotificationRow> _items = [];
    [ObservableProperty] private bool _loaded;
    [ObservableProperty] private int _unreadCount;

    public int Count => Items.Count;
    public bool HasUnread => UnreadCount > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var feed = await _api.GetNotificationsAsync();
            UnreadCount = feed.UnreadCount;
            Items = feed.Items
                .Select(n => new NotificationRow(n.Id, Icon(n.Type), n.Title, n.Body, FormatWhen(n.CreatedAt), n.Read))
                .ToList();
        }
        catch
        {
            Items = [];
        }
        finally
        {
            Loaded = true;
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasUnread));
        }
    }

    [RelayCommand]
    private async Task MarkRead(Guid id)
    {
        if (await _api.MarkNotificationReadAsync(id))
            await LoadAsync();
    }

    [RelayCommand]
    private async Task MarkAllRead()
    {
        if (await _api.MarkAllNotificationsReadAsync())
            await LoadAsync();
    }

    private static string Icon(string type) => type switch
    {
        "WaitlistSpotAvailable" => "🔔",
        "MembershipExpiring" => "⏳",
        "ReservationConfirmed" => "✅",
        "MembershipPurchased" => "💳",
        _ => "ℹ️"
    };

    private static string FormatWhen(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return $"{Days[(int)local.DayOfWeek]} {local.Day} {Months[local.Month - 1]} · {local:HH:mm}";
    }

    public record NotificationRow(Guid Id, string Icon, string Title, string Body, string When, bool Read);
}
