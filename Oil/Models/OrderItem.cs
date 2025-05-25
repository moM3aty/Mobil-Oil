using System.ComponentModel.DataAnnotations;

namespace Oil.Models
{

    public class OrderItem
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = null!;

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int Quantity { get; set; }

        public string? ImageUrl { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; } = null!;
    }
}


