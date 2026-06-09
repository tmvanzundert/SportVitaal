namespace SportVitaal.Domain.ValueObjects
{
    // Represents a seat/position in a lesson (e.g. spinning bike)
    public sealed class Seat : ValueObject
    {
        public int Row { get; }
        public int Column { get; }
        public string Id { get; }

        public Seat(int row, int column)
        {
            if (row < 1) throw new ArgumentOutOfRangeException(nameof(row));
            if (column < 1) throw new ArgumentOutOfRangeException(nameof(column));
            Row = row;
            Column = column;
            Id = $"R{Row}C{Column}";
        }

        public Seat(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Seat id must be provided", nameof(id));
            Id = id.Trim();

            // Attempt to parse simple R{row}C{col} format
            try
            {
                if (Id.StartsWith("R") && Id.Contains("C"))
                {
                    var parts = Id.Substring(1).Split('C');
                    Row = int.Parse(parts[0]);
                    Column = int.Parse(parts[1]);
                }
                else
                {
                    Row = 0; Column = 0; // unknown layout, keep id
                }
            }
            catch
            {
                Row = 0; Column = 0;
            }
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Id;
        }

        public override string ToString() => Id;
    }
}

