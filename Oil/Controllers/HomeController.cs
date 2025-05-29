using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oil.Data;
using Oil.Models;
using Oil.ViewModel;
using System.Diagnostics;

namespace Oil.Controllers
{
    public class HomeController : Controller
    {      
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult ToggleLanguage(string returnUrl)
        {
            // Get the current language from the cookies (default is Arabic)
            var currentLanguage = Request.Cookies["Language"] ?? "ar";
            var newLanguage = currentLanguage == "ar" ? "en" : "ar";

            // Set the new language cookie
            Response.Cookies.Append("Language", newLanguage, new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddYears(1),
                IsEssential = true
            });

            // Redirect to the previous page
            return LocalRedirect(returnUrl);
        }
        public IActionResult Index()
        {

            // Get all categories from the database, including related products if needed
            var categories = _context.ProductCategories.ToList();
            var products = _context.Products.ToList();
            // Pass to the view via ViewBag or ViewData
            ViewBag.Categories = categories;

            // Also set direction (example)
            ViewData["Direction"] = "rtl"; // or "ltr" based on your logic

            return View(products);
        }

        public IActionResult cartProducts()
        {
            
            return View();
        }
        public IActionResult Parts()
        {
            var types = _context.ProductTypes.ToList();
            return View(types);
        }

        public IActionResult CompanyPartsByType(int id) // 'id' هنا هو ProductTypeId
        {
            // 1. الحصول على نوع المنتج (القسم) المحدد
            var selectedProductType = _context.ProductTypes.FirstOrDefault(t => t.Id == id);
            if (selectedProductType == null)
            {
                return NotFound();
            }

            // 2. الحصول على المنتجات التي تنتمي إلى هذا القسم (ProductType) وتكون مرئية
            var productsOfType = _context.Products
                                        .Where(p => p.ProductTypeId == id && p.IsVisible)
                                        .Include(p => p.Category) // لتحميل معلومات الشركة (ProductCategory) مع المنتج
                                        .ToList();

            ViewBag.ProductsToDisplay = productsOfType;

            // 3. الحصول على جميع الشركات (ProductCategory) لاستخدامها في الفلتر العلوي
            var filterCategories = _context.ProductCategories.ToList();
            ViewBag.FilterCategories = filterCategories;

            // 4. تعيين عنوان الصفحة بناءً على اسم القسم (ProductType) المحدد
            ViewData["SelectedCategoryNameAr"] = selectedProductType.ProductTypeAr;
            ViewData["SelectedCategoryNameEn"] = selectedProductType.ProductTypeEn;

            // 5. تعيين الفلتر النشط مبدئيًا لـ "الكل" (بالنسبة لفلتر الشركات)
            ViewData["SelectedCategoryId"] = "all"; // هذا خاص بفلتر الشركات في الصفحة

            // 6. تعيين اتجاه اللغة
            ViewData["Direction"] = Request.Cookies["Language"] == "en" ? "ltr" : "rtl";

            // استخدام نفس العرض "CompanyParts.cshtml" لعرض المنتجات
            // لا يتم تمرير selectedProductType كـ model أساسي إذا كان العرض يعتمد على ViewBag/ViewData
            return View("CompanyParts");
        }


        public IActionResult CompanyParts()
        {           
            return View();
        }

        public IActionResult productDetail(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product); 
        }

        public IActionResult CategoryProducts(int id)
        {
            var category = _context.ProductCategories
                .Where(c => c.Id == id)
                .Select(c => new ProductCategory
                {
                    Id = c.Id,
                    NameAr = c.NameAr,
                    NameEn = c.NameEn,
                    ImagePath = c.ImagePath,
                    Products = c.Products.ToList()
                })
                .FirstOrDefault();

            if (category == null)
                return NotFound();

            ViewData["Direction"] = "rtl";

            return View(category);
        }


        public IActionResult Privacy()
        {
            return View();
        }
       

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
