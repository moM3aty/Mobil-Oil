using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Required for Include and FirstOrDefaultAsync, FindAsync
using Oil.Data;
using Oil.Models;
using Oil.Services;
using System;
using System.ComponentModel.DataAnnotations; // Required for Validation Attributes
using System.Text.Json;
using Microsoft.Extensions.Logging; // Required for ILogger
using System.Linq; // Required for SelectMany, Any
using System.Threading.Tasks; // Required for Task
using System.Collections.Generic; // Required for List

namespace Oil.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly ILogger<CartController> _logger;

        public CartController(ApplicationDbContext context,
                              IFileService fileService,
                              ILogger<CartController> logger)
        {
            _context = context;
            _fileService = fileService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            // This view reads from localStorage client-side
            return View();
        }

        public IActionResult Register()
        {
            // This view likely shows forms based on payment method selected in cart
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitWalletOrder([FromForm] WalletOrderRequest request) // This will now be specifically for E-Wallets like Vodafone Cash
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                return Json(new { success = false, message = "يرجى تصحيح الأخطاء التالية:", errors });
            }

            List<CartItemViewModel> cartItemsVM;
            try
            {
                cartItemsVM = JsonSerializer.Deserialize<List<CartItemViewModel>>(request.CartItemsJson ?? "[]", new JsonSerializerOptions
                { PropertyNameCaseInsensitive = true }) ?? new List<CartItemViewModel>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error in SubmitWalletOrder for cart: {CartJson}", request.CartItemsJson);
                return Json(new { success = false, message = "حدث خطأ في قراءة بيانات السلة." });
            }

            if (!cartItemsVM.Any()) return Json(new { success = false, message = "السلة فارغة." });

            string? receiptFilePath = null;
            if (request.Receipt != null && request.Receipt.Length > 0)
            {
                try { receiptFilePath = await _fileService.SaveFileAsync(request.Receipt, "receipts/ewallet", new[] { ".jpg", ".jpeg", ".png", ".pdf" }); } // Specific subfolder
                catch (ArgumentException ex) { _logger.LogWarning(ex, "File validation error (receipt) during E-Wallet order."); return Json(new { success = false, message = ex.Message }); }
                catch (Exception ex) { _logger.LogError(ex, "Error saving receipt file during E-Wallet order."); return Json(new { success = false, message = "حدث خطأ أثناء حفظ إيصال الدفع." }); }
                if (string.IsNullOrEmpty(receiptFilePath)) { return Json(new { success = false, message = "لم يتم حفظ إيصال الدفع بنجاح." }); }
            }
            else { return Json(new { success = false, message = "إيصال الدفع مطلوب." }); }

            var order = new Order
            {
                PaymentMethod = "E-Wallet", // Specific payment method
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                CreatedAt = DateTime.UtcNow,
                ReceiptFileName = receiptFilePath
            };

            // ... (Loop through cartItemsVM and add OrderItems, checking productFromDb as before) ...
            foreach (var itemVM in cartItemsVM)
            {
                var productFromDb = await _context.Products.FindAsync(itemVM.ProductId);
                if (productFromDb == null)
                {
                    if (!string.IsNullOrEmpty(receiptFilePath)) _fileService.DeleteFile(receiptFilePath);
                    return Json(new { success = false, message = $"المنتج '{itemVM.Title}' لم يعد متوفرًا." });
                }
                order.OrderItems.Add(new OrderItem { Title = productFromDb.TitleAr, Price = productFromDb.Price, Quantity = itemVM.Quantity, ImageUrl = productFromDb.ImageUrl });
            }

            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                return Json(new { success = true, redirectUrl = Url.Action("OrderConfirmation", "Cart", new { id = order.Id }) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving E-Wallet order to DB for user {UserName}", request.Name);
                if (!string.IsNullOrEmpty(receiptFilePath)) _fileService.DeleteFile(receiptFilePath);
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء حفظ الطلب." });
            }
        }

        // New Action for Instapay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitInstapayOrder([FromForm] InstapayOrderRequest request) // New DTO for Instapay
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                return Json(new { success = false, message = "يرجى تصحيح الأخطاء التالية:", errors });
            }

            List<CartItemViewModel> cartItemsVM;
            try { cartItemsVM = JsonSerializer.Deserialize<List<CartItemViewModel>>(request.CartItemsJson ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CartItemViewModel>(); }
            catch (JsonException ex) { _logger.LogError(ex, "JSON deserialization error in SubmitInstapayOrder for cart: {CartJson}", request.CartItemsJson); return Json(new { success = false, message = "حدث خطأ في قراءة بيانات السلة." }); }
            if (!cartItemsVM.Any()) return Json(new { success = false, message = "السلة فارغة." });

            string? receiptFilePath = null;
            if (request.Receipt != null && request.Receipt.Length > 0)
            {
                try { receiptFilePath = await _fileService.SaveFileAsync(request.Receipt, "receipts/instapay", new[] { ".jpg", ".jpeg", ".png", ".pdf" }); } // Specific subfolder
                catch (ArgumentException ex) { _logger.LogWarning(ex, "File validation error (receipt) during Instapay order."); return Json(new { success = false, message = ex.Message }); }
                catch (Exception ex) { _logger.LogError(ex, "Error saving receipt file during Instapay order."); return Json(new { success = false, message = "حدث خطأ أثناء حفظ إيصال الدفع." }); }
                if (string.IsNullOrEmpty(receiptFilePath)) { return Json(new { success = false, message = "لم يتم حفظ إيصال الدفع بنجاح." }); }
            }
            else { return Json(new { success = false, message = "إيصال الدفع مطلوب." }); }


            var order = new Order
            {
                PaymentMethod = "Instapay", // Specific payment method
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                CreatedAt = DateTime.UtcNow,
                ReceiptFileName = receiptFilePath
            };

            foreach (var itemVM in cartItemsVM)
            {
                var productFromDb = await _context.Products.FindAsync(itemVM.ProductId);
                if (productFromDb == null)
                {
                    if (!string.IsNullOrEmpty(receiptFilePath)) _fileService.DeleteFile(receiptFilePath);
                    return Json(new { success = false, message = $"المنتج '{itemVM.Title}' لم يعد متوفرًا." });
                }
                order.OrderItems.Add(new OrderItem { Title = productFromDb.TitleAr, Price = productFromDb.Price, Quantity = itemVM.Quantity, ImageUrl = productFromDb.ImageUrl });
            }

            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                return Json(new { success = true, redirectUrl = Url.Action("OrderConfirmation", "Cart", new { id = order.Id }) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Instapay order to DB for user {UserName}", request.Name);
                if (!string.IsNullOrEmpty(receiptFilePath)) _fileService.DeleteFile(receiptFilePath);
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء حفظ الطلب." });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitCashOrder([FromForm] CashOrderSubmitRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                return Json(new { success = false, message = "يرجى تصحيح الأخطاء التالية:", errors });
            }

            List<CartItemViewModel> cartItemsVM;
            try
            {
                cartItemsVM = JsonSerializer.Deserialize<List<CartItemViewModel>>(request.CartItemsJson ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CartItemViewModel>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error in SubmitCashOrder for cart: {CartJson}", request.CartItemsJson);
                return Json(new { success = false, message = "حدث خطأ في قراءة بيانات السلة." });
            }
            if (!cartItemsVM.Any()) return Json(new { success = false, message = "السلة فارغة." });

            string? idImageFilePath = null;
            if (request.IdImage != null && request.IdImage.Length > 0)
            {
                try
                {
                    idImageFilePath = await _fileService.SaveFileAsync(request.IdImage, "id_images", new[] { ".jpg", ".jpeg", ".png" });
                }
                catch (ArgumentException ex) { _logger.LogWarning(ex, "File validation error (ID image) during cash order."); return Json(new { success = false, message = ex.Message }); }
                catch (Exception ex) { _logger.LogError(ex, "Error saving ID image file during cash order."); return Json(new { success = false, message = "حدث خطأ أثناء حفظ صورة الهوية." }); }

                if (string.IsNullOrEmpty(idImageFilePath))
                {
                    return Json(new { success = false, message = "لم يتم حفظ صورة الهوية بنجاح." });
                }
            }
            else if (request.IdImage == null || request.IdImage.Length == 0) // IdImage is required
            {
                return Json(new { success = false, message = "صورة الهوية مطلوبة." });
            }

            var order = new Order
            {
                PaymentMethod = "Cash",
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                IdImageFileName = idImageFilePath,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var itemVM in cartItemsVM)
            {
                var productFromDb = await _context.Products.FindAsync(itemVM.ProductId);
                if (productFromDb == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found in DB for cash order. Item: {ItemTitle}", itemVM.ProductId, itemVM.Title);
                    if (!string.IsNullOrEmpty(idImageFilePath)) _fileService.DeleteFile(idImageFilePath);
                    return Json(new { success = false, message = $"المنتج '{itemVM.Title}' لم يعد متوفرًا. يرجى تحديث سلتك." });
                }
                order.OrderItems.Add(new OrderItem
                {
                    Title = productFromDb.TitleAr,
                    Price = productFromDb.Price,
                    Quantity = itemVM.Quantity,
                    ImageUrl = productFromDb.ImageUrl
                });
            }

            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                return Json(new { success = true, redirectUrl = Url.Action("OrderConfirmation", "Cart", new { id = order.Id }) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cash order to DB for user {UserName}", request.Name);
                if (!string.IsNullOrEmpty(idImageFilePath)) _fileService.DeleteFile(idImageFilePath);
                return StatusCode(500, new { success = false, message = "حدث خطأ أثناء حفظ الطلب." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitVisaOrder([FromForm] VisaOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "بيانات طلب الفيزا غير كاملة أو غير صحيحة.";
                // Consider returning JSON if this is called via AJAX, similar to other methods
                // For now, keeping redirect as per original structure for this specific action
                return RedirectToAction("Register");
            }

            List<CartItemViewModel> cartItemsVM;
            try { cartItemsVM = JsonSerializer.Deserialize<List<CartItemViewModel>>(request.CartItemsJson ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CartItemViewModel>(); }
            catch (JsonException ex) { _logger.LogError(ex, "JSON Error Visa Order: {CartJson}", request.CartItemsJson); TempData["ErrorMessage"] = "خطأ في بيانات السلة لطلب الفيزا."; return RedirectToAction("Register"); }
            if (!cartItemsVM.Any()) { TempData["ErrorMessage"] = "السلة فارغة لطلب الفيزا."; return RedirectToAction("Register"); }

            string? confirmationFilePath = null;
            if (request.ConfirmationFile != null && request.ConfirmationFile.Length > 0)
            {
                try
                {
                    confirmationFilePath = await _fileService.SaveFileAsync(request.ConfirmationFile, "visa_confirmations", new[] { ".jpg", ".jpeg", ".png", ".pdf" });
                }
                catch (ArgumentException ex) { _logger.LogWarning(ex, "File validation error (Visa confirmation) during manual Visa order."); TempData["ErrorMessage"] = ex.Message; return RedirectToAction("Register"); }
                catch (Exception ex) { _logger.LogError(ex, "Error saving Visa confirmation file."); TempData["ErrorMessage"] = "حدث خطأ أثناء حفظ ملف تأكيد الفيزا."; return RedirectToAction("Register"); }

                if (string.IsNullOrEmpty(confirmationFilePath))
                {
                    TempData["ErrorMessage"] = "لم يتم حفظ ملف تأكيد الفيزا بنجاح."; return RedirectToAction("Register");
                }
            } // Note: ConfirmationFile is optional in VisaOrderRequest, so no error if it's null/empty and not provided.

            var order = new Order
            {
                PaymentMethod = "Visa (Manual)",
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                ConfirmationFileName = confirmationFilePath,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var itemVM in cartItemsVM)
            {
                var productFromDb = await _context.Products.FindAsync(itemVM.ProductId);
                if (productFromDb == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found in DB for manual Visa order. Item: {ItemTitle}", itemVM.ProductId, itemVM.Title);
                    if (!string.IsNullOrEmpty(confirmationFilePath)) _fileService.DeleteFile(confirmationFilePath);
                    TempData["ErrorMessage"] = $"المنتج '{itemVM.Title}' لم يعد متوفرًا. يرجى تحديث سلتك.";
                    return RedirectToAction("Register");
                }
                order.OrderItems.Add(new OrderItem
                {
                    Title = productFromDb.TitleAr,
                    Price = productFromDb.Price,
                    Quantity = itemVM.Quantity,
                    ImageUrl = productFromDb.ImageUrl
                });
            }
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                return RedirectToAction("OrderConfirmation", new { id = order.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Visa (manual) order to DB for {UserName}", request.Name);
                if (!string.IsNullOrEmpty(confirmationFilePath)) _fileService.DeleteFile(confirmationFilePath);
                TempData["ErrorMessage"] = "حدث خطأ أثناء حفظ طلب الفيزا."; return RedirectToAction("Register");
            }
        }

        public async Task<IActionResult> OrderConfirmation(int id)
        {
            var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) { TempData["ErrorMessage"] = "لم يتم العثور على الطلب."; return RedirectToAction("Index", "Home"); }
            return View(order);
        }
    }

    // --- DTOs (Data Transfer Objects) / ViewModels for CartController ---

    public class WalletOrderRequest
    {
        [Required(ErrorMessage = "الاسم مطلوب")]
        public string? Name { get; set; }
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم هاتف غير صالح")]
        public string? Phone { get; set; }
        [Required(ErrorMessage = "العنوان مطلوب")]
        public string? Address { get; set; }
        [Required(ErrorMessage = "إيصال الدفع مطلوب")]
        public IFormFile? Receipt { get; set; }
        [Required(ErrorMessage = "بيانات السلة مطلوبة")]
        public string? CartItemsJson { get; set; }
    }

    public class CashOrderSubmitRequest
    {
        [Required(ErrorMessage = "الاسم مطلوب")]
        public string? Name { get; set; }
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم هاتف غير صالح")]
        public string? Phone { get; set; }
        [Required(ErrorMessage = "العنوان مطلوب")]
        public string? Address { get; set; }
        [Required(ErrorMessage = "صورة الهوية مطلوبة")]
        public IFormFile? IdImage { get; set; }
        [Required(ErrorMessage = "بيانات السلة مطلوبة")]
        public string? CartItemsJson { get; set; }
    }
    public class InstapayOrderRequest
    {
        [Required(ErrorMessage = "الاسم مطلوب")]
        public string? Name { get; set; }
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم هاتف غير صالح")]
        public string? Phone { get; set; }
        [Required(ErrorMessage = "العنوان مطلوب")]
        public string? Address { get; set; }
        [Required(ErrorMessage = "إيصال الدفع مطلوب")]
        public IFormFile? Receipt { get; set; }
        [Required(ErrorMessage = "بيانات السلة مطلوبة")]
        public string? CartItemsJson { get; set; }
    }
    public class VisaOrderRequest
    {
        [Required(ErrorMessage = "الاسم مطلوب")]
        public string? Name { get; set; }
        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم هاتف غير صالح")]
        public string? Phone { get; set; }
        [Required(ErrorMessage = "العنوان مطلوب")]
        public string? Address { get; set; }
        public IFormFile? ConfirmationFile { get; set; } // Optional file for manual visa
        [Required(ErrorMessage = "بيانات السلة مطلوبة")]
        public string? CartItemsJson { get; set; }
    }

    // ViewModel for items coming from client-side cart (localStorage)
    public class CartItemViewModel
    {
        [Required(ErrorMessage = "معرف المنتج مطلوب في السلة")]
        [Range(1, int.MaxValue, ErrorMessage = "معرف المنتج غير صالح في السلة.")]
        public int ProductId { get; set; }

        public string? Title { get; set; }
        public decimal Price { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون 1 على الأقل")]
        public int Quantity { get; set; }
        public string? ImageUrl { get; set; }
    }

}