using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oil.Data;
using Oil.Models;
using Oil.Services;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering; // For SelectList
using Microsoft.Extensions.Logging;

namespace Oil.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(ApplicationDbContext context, IFileService fileService, ILogger<ProductsController> logger)
        {
            _context = context;
            _fileService = fileService;
            _logger = logger;
        }

        private bool IsLoggedIn() => !string.IsNullOrEmpty(HttpContext.Session.GetString("Username"));

        private void PopulateDropdowns(Product? product = null) // Optionally pass product for Edit to set selected
        {
            ViewBag.Categories = new SelectList(_context.ProductCategories.AsNoTracking().ToList(), "Id", "NameAr", product?.CategoryId);
            ViewBag.ProductTypes = new SelectList(_context.ProductTypes.AsNoTracking().ToList(), "Id", "ProductTypeAr", product?.ProductTypeId);
        }

        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductType)
                .AsNoTracking()
                .ToListAsync();
            return View(products);
        }

        public IActionResult Create()
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            PopulateDropdowns();
            return View(new Product { IsVisible = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");

            ModelState.Remove("ImageUrl");
            ModelState.Remove("Category");
            ModelState.Remove("ProductType");

            if (ModelState.IsValid)
            {
                if (imageFile != null)
                {
                    try
                    {
                        product.ImageUrl = await _fileService.SaveFileAsync(imageFile, "products", new[] { ".jpg", ".jpeg", ".png", ".gif" });
                    }
                    catch (ArgumentException ex) { ModelState.AddModelError("imageFile", ex.Message); PopulateDropdowns(product); return View(product); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving product image for {ProductName}", product.TitleAr);
                        ModelState.AddModelError("", "خطأ في رفع الصورة.");
                        PopulateDropdowns(product);
                        return View(product);
                    }
                }
                else
                {
                    product.ImageUrl = "/images/products/default.png";
                }

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PopulateDropdowns(product);
            return View(product);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            PopulateDropdowns(product); // استدعاء مع تمرير المنتج لتحديد القيم المختارة
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? imageFile)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            if (id != product.Id) return NotFound();

            ModelState.Remove("ImageUrl");
            ModelState.Remove("Category");
            ModelState.Remove("ProductType");

            if (ModelState.IsValid)
            {
                var productToUpdate = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                if (productToUpdate == null) return NotFound();

                string? oldImagePath = productToUpdate.ImageUrl;

                if (imageFile != null)
                {
                    try
                    {
                        product.ImageUrl = await _fileService.SaveFileAsync(imageFile, "products", new[] { ".jpg", ".jpeg", ".png", ".gif" });
                        if (!string.IsNullOrEmpty(product.ImageUrl) &&
                            !string.IsNullOrEmpty(oldImagePath) &&
                            oldImagePath != "/images/products/default.png")
                        {
                            _fileService.DeleteFile(oldImagePath);
                        }
                    }
                    catch (ArgumentException ex) { ModelState.AddModelError("imageFile", ex.Message); PopulateDropdowns(product); return View(product); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating product image for {ProductId}", id);
                        ModelState.AddModelError("", "خطأ في تحديث الصورة.");
                        PopulateDropdowns(product); return View(product);
                    }
                }
                else
                {
                    product.ImageUrl = oldImagePath;
                }

                try
                {
                    
                    _context.Update(product); 
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException) { if (!ProductExists(product.Id)) return NotFound(); else throw; }
                return RedirectToAction(nameof(Index));
            }
            PopulateDropdowns(product);
            return View(product);
        }

        public async Task<IActionResult> Delete(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            string? imagePathToDelete = product.ImageUrl;
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            if (!string.IsNullOrEmpty(imagePathToDelete) && imagePathToDelete != "/images/products/default.png")
            {
                _fileService.DeleteFile(imagePathToDelete);
            }
            return RedirectToAction(nameof(Index));
        }
        private bool ProductExists(int id) => _context.Products.Any(e => e.Id == id);
    }
}
