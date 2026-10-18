namespace OrderService.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ReservationId { get; set; }
    }
}