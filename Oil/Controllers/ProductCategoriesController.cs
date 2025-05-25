using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oil.Data;
using Oil.Models;
using Oil.Services; // Add this
using Microsoft.AspNetCore.Http; // For IFormFile
using System.Threading.Tasks; // For Task
using Microsoft.Extensions.Logging; // Optional

namespace Oil.Controllers
{
    public class ProductCategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        // private readonly IWebHostEnvironment _webHostEnvironment; // No longer needed directly
        private readonly IFileService _fileService;
        private readonly ILogger<ProductCategoriesController> _logger; // Optional

        public ProductCategoriesController(ApplicationDbContext context,
                                           /* IWebHostEnvironment webHostEnvironment, // Remove */
                                           IFileService fileService,
                                           ILogger<ProductCategoriesController> logger)
        {
            _context = context;
            // _webHostEnvironment = webHostEnvironment;
            _fileService = fileService;
            _logger = logger;
        }

        private bool IsLoggedIn() => !string.IsNullOrEmpty(HttpContext.Session.GetString("Username"));

        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login"); // Corrected redirect
            return View(await _context.ProductCategories.ToListAsync());
        }

        public IActionResult Create()
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductCategory category, IFormFile? ImageFile)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            if (ModelState.IsValid)
            {
                if (ImageFile != null)
                {
                    try
                    {
                        // Use "categories" as subfolder for category images
                        category.ImagePath = await _fileService.SaveFileAsync(ImageFile, "categories", new[] { ".jpg", ".jpeg", ".png", ".gif" });
                    }
                    catch (ArgumentException ex) { ModelState.AddModelError("ImageFile", ex.Message); return View(category); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving category image for {CategoryName}", category.NameAr);
                        ModelState.AddModelError("", "حدث خطأ أثناء رفع الصورة.");
                        return View(category);
                    }
                }
                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            var category = await _context.ProductCategories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductCategory updatedCategory, IFormFile? ImageFile)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            if (id != updatedCategory.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var categoryToUpdate = await _context.ProductCategories.FindAsync(id);
                if (categoryToUpdate == null) return NotFound();

                string? oldImagePath = categoryToUpdate.ImagePath; // Keep track of old image

                if (ImageFile != null)
                {
                    try
                    {
                        updatedCategory.ImagePath = await _fileService.SaveFileAsync(ImageFile, "categories", new[] { ".jpg", ".jpeg", ".png", ".gif" });
                        if (!string.IsNullOrEmpty(updatedCategory.ImagePath) && !string.IsNullOrEmpty(oldImagePath))
                        {
                            _fileService.DeleteFile(oldImagePath); // Delete old image only if new one is saved
                        }
                    }
                    catch (ArgumentException ex) { ModelState.AddModelError("ImageFile", ex.Message); return View(updatedCategory); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating category image for {CategoryId}", id);
                        ModelState.AddModelError("", "حدث خطأ أثناء تحديث الصورة.");
                        return View(updatedCategory); // Return view with model to show error
                    }
                }
                else
                {
                    updatedCategory.ImagePath = oldImagePath; // Keep old image if no new one uploaded
                }

                categoryToUpdate.NameAr = updatedCategory.NameAr;
                categoryToUpdate.NameEn = updatedCategory.NameEn;
                categoryToUpdate.ImagePath = updatedCategory.ImagePath; // Assign new or old path

                try
                {
                    _context.Update(categoryToUpdate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException) { if (!ProductCategoryExists(updatedCategory.Id)) return NotFound(); else throw; }
                return RedirectToAction(nameof(Index));
            }
            return View(updatedCategory);
        }

        public async Task<IActionResult> Delete(int id) // Should be POST for destructive operations
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            var category = await _context.ProductCategories.FindAsync(id);
            if (category == null) return NotFound();

            // Store path before deleting from DB
            string? imagePathToDelete = category.ImagePath;

            _context.ProductCategories.Remove(category);
            await _context.SaveChangesAsync();

            // Delete file after successful DB deletion
            if (!string.IsNullOrEmpty(imagePathToDelete))
            {
                _fileService.DeleteFile(imagePathToDelete);
            }
            return RedirectToAction(nameof(Index));
        }
        private bool ProductCategoryExists(int id) => _context.ProductCategories.Any(e => e.Id == id);
    }
}