using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Oil.Models
{
    public class ShippingZone
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنطقة (عربي) مطلوب")]
        [Display(Name = "اسم المنطقة (عربي)")]
        public string NameAr { get; set; }

        [Required(ErrorMessage = "اسم المنطقة (إنجليزي) مطلوب")]
        [Display(Name = "اسم المنطقة (إنجليزي)")]
        public string NameEn { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true; 

        public virtual ShippingCost ShippingCost { get; set; }
    }
}
