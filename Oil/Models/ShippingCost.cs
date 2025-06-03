using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Oil.Models
{
    public class ShippingCost
    {
        [Key]
        [ForeignKey("ShippingZone")] 
        public int ShippingZoneId { get; set; }

        [Required(ErrorMessage = "سعر الشحن مطلوب")]
        [DataType(DataType.Currency)]
        [Column(TypeName = "decimal(18, 2)")]
        [Display(Name = "سعر الشحن")]
        public decimal Cost { get; set; }

        public virtual ShippingZone ShippingZone { get; set; }
    }
}
