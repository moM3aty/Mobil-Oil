// Order.cs - Remains the same as previous version with PaymobOrderId
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Oil.Models
{
    public class Order
    {
        public int Id { get; set; }
        [Required]
        public string PaymentMethod { get; set; } = null!;
        [Required]
        public string Name { get; set; } = null!;
        [Required]
        [Phone]
        public string Phone { get; set; } = null!;
        [Required]
        public string Address { get; set; } = null!;
        public string? ConfirmationFileName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? IdImageFileName { get; set; }
        public string? ReceiptFileName { get; set; }
        public int? PaymobOrderId { get; set; }
        public virtual List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public int? ShippingZoneId { get; set; } 
        public virtual ShippingZone SelectedShippingZone { get; set; }

        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? ShippingFee { get; set; } 

    }
}
