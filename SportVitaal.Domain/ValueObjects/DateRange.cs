namespace SportVitaal.Domain.ValueObjects
{
    public sealed class DateRange : ValueObject
    {
        public DateTime Start { get; }
        public DateTime End { get; }

        public DateRange(DateTime start, DateTime end)
        {
            if (end < start) throw new ArgumentException("End must be greater than or equal to Start");
            Start = start;
            End = end;
        }

        public bool Contains(DateTime moment) => moment >= Start && moment <= End;

        public bool Overlaps(DateRange other)
        {
            if (other == null) return false;
            return Start <= other.End && other.Start <= End;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Start;
            yield return End;
        }

        public override string ToString() => $"{Start:o}/{End:o}";
    }
}

