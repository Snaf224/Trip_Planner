namespace Diplom.Models
{
    public class TripSearch
    {
        public string Name { get; set; }
        public DateTime? TravelDate { get; set; } = DateTime.Today;
        public decimal Budget { get; set; }
        public int NumberOfPeople { get; set; }
        public string Email { get; set; }
    }
}
