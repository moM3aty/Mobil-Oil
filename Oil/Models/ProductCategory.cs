using System.ComponentModel.DataAnnotations;

namespace Oil.Models
{
    public class ProductCategory
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الشركه باللغة العربية مطلوب")]
        [Display(Name = "اسم الشركه عربي")]
        public string NameAr { get; set; }

        [Required(ErrorMessage = "اسم الشركه باللغة الانجليزيه مطلوب")]
        [Display(Name = "اسم الشركه انجليزي")]
        public string NameEn { get; set; }

        [Display(Name = "مسار الصورة")]
        public string? ImagePath { get; set; }
        
        public ICollection<Product>? Products { get; set; }
    }
}
