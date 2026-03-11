namespace src.Models
{
    public class Scan
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; } // Foreign Key
        public int MountainId { get; set; }
        public DateTime Timestamp { get; set; }

        // Navigation property (Optional, but recommended for joins)
        public virtual User User { get; set; }
    }
}
