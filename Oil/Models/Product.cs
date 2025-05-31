using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Oil.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المنتج باللغة العربية مطلوب")]
        [Display(Name = "اسم المنتج (عربي)")]
        public string TitleAr { get; set; }

        [Required(ErrorMessage = "اسم المنتج باللغه الانجليزيه مطلوب")]
        [Display(Name = "اسم المنتج (انجليزي)")]
        public string TitleEn { get; set; }

        [Display(Name = "الوصف (عربي)")]
        public string? DescriptionAr { get; set; }

        [Display(Name = "الوصف (انجليزي)")]
        public string? DescriptionEn { get; set; }

        [Display(Name = "النوع (عربي)")]
        public string? TypeAr { get; set; }

        [Display(Name = "النوع (انجليزي)")]
        public string? TypeEn { get; set; }

        [Display(Name = "اللون (عربي)")]
        public string? ColorAr { get; set; }

        [Display(Name = "اللون (انجليزي)")]
        public string? ColorEn { get; set; }

        [Display(Name = "السعة (عربي)")]
        public string? CapacityAr { get; set; }

        [Display(Name = "السعه (انجليزي)")]
        public string? CapacityEn { get; set; }

        [Display(Name = "اللزوجة (عربي)")]
        public string? ViscosityAr { get; set; }

        [Display(Name = "اللزوجه (انجليزي)")]
        public string? ViscosityEn { get; set; }

        [Required(ErrorMessage = "السعر مطلوب")]
        [Display(Name = "السعر")]
        public decimal Price { get; set; }

        [Display(Name = "الوزن (عربي)")]
        public string? WeightAr { get; set; }

        [Display(Name = "الوزن (انجليزي)")]
        public string? WeightEn { get; set; }

        [Display(Name = "الأبعاد (عربي)")]
        public string? DimensionsAr { get; set; }

        [Display(Name = "الأبعاد (انجليزي)")]
        public string? DimensionsEn { get; set; }

        [Display(Name = "رابط الصورة")]
        public string? ImageUrl { get; set; }

        [Required(ErrorMessage = "يجب تحديد الشركه")]
        [Display(Name = "الشركه")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public ProductCategory? Category { get; set; }

        [Display(Name = "العلامة التجارية (عربي)")]
        public string? BrandAr { get; set; }

        [Display(Name = "العلامه التجاريه (انجليزي)")]
        public string? BrandEn { get; set; }

        [Display(Name = "الشركة المصنعة (عربي)")]
        public string? ManufacturerAr { get; set; }

        [Display(Name = "الشركه المصنعه (انجليزي)")]
        public string? ManufacturerEn { get; set; }

        [Display(Name = "السعر قبل الخصم")]
        [Column(TypeName = "decimal(18,2)")] 
        public decimal? PriceBeforeDiscount { get; set; }

        [Display(Name = "عرض المنتج")]
        public bool IsVisible { get; set; } = true;
        public int ProductTypeId { get; set; }

        [ForeignKey("ProductTypeId")]
        public ProductType? ProductType { get; set; }

    }
}
