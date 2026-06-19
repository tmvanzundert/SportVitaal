using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportVitaal.Services;

namespace SportVitaal.ViewModels;

/// <summary>
/// Backs the "Mijn Abonnement" page: the member's current plan and payment info, the available
/// plans, and the buy / renew / upgrade / cancel actions. Prices/features mirror the API's table.
/// </summary>
public partial class SubscriptionViewModel : ObservableObject
{
    private static readonly string[] Months =
        { "jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec" };

    private readonly ApiClient _api;

    public SubscriptionViewModel(ApiClient api) => _api = api;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _messageIsError;

    private ApiClient.MembershipDto? M => _api.CurrentUser?.Membership;

    public bool HasPlan => M is not null && M.Type is not "None";
    public bool IsUnlimited => M?.Type is "UnlimitedMonthly" or "UnlimitedYearly";
    public bool IsMonthly => M?.Type is "TwiceWeeklyMonthly" or "UnlimitedMonthly";

    public string TierName => IsUnlimited ? "Onbeperkt" : "2x per week";
    public string Billing => M?.Type is "TwiceWeeklyYearly" or "UnlimitedYearly" ? "Jaarlijks" : "Maandelijks";
    public string CurrentSummary => HasPlan ? $"{TierName} · {Billing}" : "Geen abonnement";

    public string Bedrag => $"€{Price:0},00";
    public string VolgendeBetaling => M?.EndDate is { } end ? FormatDate(end) : "—";

    // No stored-payment-method API yet; shown as a static placeholder matching the design.
    public string PaymentMethod = "•••• 4821";

    /// <summary>Only the "2x per week" tiers can be upgraded, and only while active.</summary>
    public bool CanUpgrade => HasPlan && !IsUnlimited && (M?.IsActive ?? false);

    /// <summary>Monthly subscriptions can be cancelled; yearly cannot.</summary>
    public bool CanCancel => HasPlan && IsMonthly;

    private int Price => M?.Type switch
    {
        "TwiceWeeklyMonthly" => 29,
        "TwiceWeeklyYearly" => 299,
        "UnlimitedMonthly" => 55,
        "UnlimitedYearly" => 549,
        _ => 0
    };

    /// <summary>The two selectable monthly tiers, with the member's current tier flagged.</summary>
    public IReadOnlyList<PlanOption> Plans =>
    [
        new("2x per week", MembershipType.TwiceWeeklyMonthly, 29,
            ["2 lessen per week", "Toegang alle groepslessen", "App-reserveringen"], !IsUnlimited && HasPlan),
        new("Onbeperkt", MembershipType.UnlimitedMonthly, 55,
            ["Onbeperkt lessen", "Toegang alle groepslessen", "App-reserveringen", "Gratis guestpass (1/mnd)"], IsUnlimited)
    ];

    [RelayCommand]
    private Task Upgrade() => RunAsync(_api.UpgradeAsync(), "Je abonnement is uitgebreid naar Onbeperkt.");

    [RelayCommand]
    private Task Renew() => RunAsync(_api.RenewAsync(), "Je abonnement is verlengd met een periode.");

    [RelayCommand]
    private Task Cancel() => RunAsync(_api.CancelAsync(), "Je abonnement is opgezegd en loopt af aan het einde van de periode.");

    [RelayCommand]
    private Task Purchase(PlanOption plan) => RunAsync(_api.PurchaseAsync(plan.Type), $"Je hebt het {plan.Name}-abonnement aangeschaft.");

    private async Task RunAsync(Task<string?> action, string successMessage)
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = null;
        try
        {
            var error = await action;
            MessageIsError = error is not null;
            Message = error ?? successMessage;
        }
        catch
        {
            MessageIsError = true;
            Message = "Er ging iets mis. Probeer het later opnieuw.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(string.Empty); // the membership changed: refresh all computed properties
        }
    }

    private static string FormatDate(DateTime d) => $"{d.Day} {Months[d.Month - 1]} {d.Year}";

    public sealed record PlanOption(string Name, MembershipType Type, int Price, string[] Features, bool IsCurrent);
}
