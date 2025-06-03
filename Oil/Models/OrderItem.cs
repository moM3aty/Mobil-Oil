using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // ما زال مطلوبًا لـ [Column]

namespace Oil.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        // خاصية المفتاح الخارجي لجدول المنتجات
        // سيتم التعرف عليها كمفتاح خارجي بواسطة اصطلاحات Entity Framework Core
        [Required(ErrorMessage = "معرف المنتج مطلوب في عنصر الطلب")]
        public int ProductId { get; set; }

        // خاصية التنقل إلى المنتج.
        // يمكن حذف السمة [ForeignKey("ProductId")] إذا تم اتباع الاصطلاحات.
        // سيفهم Entity Framework Core أن ProductId هي المفتاح الخارجي لـ Product.
        public virtual Product? Product { get; set; }

        [Required(ErrorMessage = "اسم المنتج في عنصر الطلب مطلوب")]
        public string Title { get; set; } = null!; // اسم المنتج وقت الطلب

        [Required(ErrorMessage = "سعر المنتج في عنصر الطلب مطلوب")]
        [Column(TypeName = "decimal(18, 2)")] // لتخزين السعر بدقة
        public decimal Price { get; set; } // سعر المنتج وقت الطلب

        [Required(ErrorMessage = "كمية المنتج في عنصر الطلب مطلوبة")]
        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون 1 على الأقل")]
        public int Quantity { get; set; }

        public string? ImageUrl { get; set; } // رابط صورة المنتج وقت الطلب

        // خاصية المفتاح الخارجي لجدول الطلبات
        [Required]
        public int OrderId { get; set; }
        public virtual Order Order { get; set; } = null!; // خاصية التنقل إلى الطلب
    }
}
