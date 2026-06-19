namespace SportVitaal.Domain.Enums
{
    /// <summary>
    /// Kinds of in-app notification shown to a member, so the client can pick an icon/route.
    /// </summary>
    public enum NotificationType
    {
        General = 0,
        WaitlistSpotAvailable = 1,
        MembershipExpiring = 2,
        ReservationConfirmed = 3,
        MembershipPurchased = 4
    }
}
