namespace SportVitaal.Domain.Entities
{
    public class WaitingListEntry : BaseEntity
    {
        // This fixes the database not being able to process the attributes via Pomelo.
        // We want to keep the properties private set to enforce invariants,
        // but EF needs a parameterless constructor.
        protected WaitingListEntry() { }
        public Guid LessonId { get; private set; }
        public Guid MemberId { get; private set; }

        public WaitingListEntry(Guid lessonId, Guid memberId)
        {
            LessonId = lessonId;
            MemberId = memberId;
            // CreatedAt is provided by BaseEntity
        }
    }
}


