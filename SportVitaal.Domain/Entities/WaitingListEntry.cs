namespace SportVitaal.Domain.Entities
{
    public class WaitingListEntry : BaseEntity
    {
        // EF needs a parameterless constructor; keep setters private to enforce invariants.
        protected WaitingListEntry() { }
        public Guid LessonId { get; private set; }
        public Guid MemberId { get; private set; }

        public WaitingListEntry(Guid lessonId, Guid memberId)
        {
            LessonId = lessonId;
            MemberId = memberId;
        }
    }
}


