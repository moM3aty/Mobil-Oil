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
        public IActionResult PickupRegistration()
        {
            return View();
        }

        // POST: /Cart/SubmitPickupOrder
        // هذا الأكشن سيستقبل البيانات من فورم الاستلام من الفرع
        [HttpPost]
        public async Task<IActionResult> SubmitPickupOrder([FromBody] PickupOrderViewModel model)
        {
            if (model == null || !ModelState.IsValid || model.CartItems == null || !model.CartItems.Any())
            {
                return Json(new { success = false, message = "البيانات غير صالحة أو السلة فارغة." });
            }

            var order = new Order
            {
                PaymentMethod = "Pickup",
                Name = model.FullName,
                Phone = model.PhoneNumber,
                Address = "استلام من الفرع: حدائق الأهرام - 236 و شارع الضغط العالي بجوار كافية الكرنك",
                CreatedAt = DateTime.UtcNow,
                ShippingFee = 0,
                ShippingZoneId = null,
                SelectedShippingZone = null,
                ReceiptFileName = null,
                IdImageFileName = null,
                ConfirmationFileName = null
            };

            foreach (var itemVM in model.CartItems)
            {
                var productFromDb = await _context.Products.FindAsync(itemVM.ProductId);
                if (productFromDb == null)
                {
                    _logger.LogWarning("Product with ID {ProductId} not found for pickup order.", itemVM.ProductId);
                    return Json(new { success = false, message = $"للأسف، المنتج '{itemVM.Title}' لم يعد متوفرًا." });
                }

                order.OrderItems.Add(new OrderItem
                {
                    ProductId = productFromDb.Id,
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

                var confirmationUrl = Url.Action("OrderConfirmation", "Cart", new { id = order.Id });
                return Json(new { success = true, redirectUrl = confirmationUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving pickup order to DB for user {UserName}", model.FullName);
                return StatusCode(500, new { success = false, message = "حدث خطأ فني أثناء تأكيد الطلب. يرجى المحاولة مرة أخرى." });
            }
        }
        public async Task<IActionResult> Register()
        {
            var activeShippingZones = await _context.ShippingZones
                                             .Include(sz => sz.ShippingCost)
                                             .Where(sz => sz.IsActive && sz.ShippingCost != null && sz.ShippingCost.Cost >= 0)
                                             .OrderBy(sz => sz.NameAr) // Or NameEn based on current language preference
                                             .Select(sz => new ShippingZoneViewModel // Use a ViewModel
                                             {
                                                 Id = sz.Id,
                                                 NameAr = sz.NameAr,
                                                 NameEn = sz.NameEn,
                                                 Cost = sz.ShippingCost != null ? sz.ShippingCost.Cost : 0
                                             })
                                             .ToListAsync();
            ViewBag.ShippingZones = activeShippingZones;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitWalletOrder([FromForm] WalletOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                return Json(new { success = false, message = "يرجى تصحيح الأخطاء التالية:", errors });
            }

            // Validate ShippingZoneId and get ShippingFee from DB
            var shippingZone = await _context.ShippingZones
                                        .Include(sz => sz.ShippingCost)
                                        .FirstOrDefaultAsync(sz => sz.Id == request.ShippingZoneId && sz.IsActive && sz.ShippingCost != null);

            if (shippingZone == null || shippingZone.ShippingCost == null)
            {
                return Json(new { success = false, message = "منطقة الشحن المختارة غير صالحة أو غير متوفرة." });
            }
            decimal actualShippingFee = shippingZone.ShippingCost.Cost;

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
                try { receiptFilePath = await _fileService.SaveFileAsync(request.Receipt, "receipts/ewallet", new[] { ".jpg", ".jpeg", ".png", ".pdf" }); }
                catch (ArgumentException ex) { _logger.LogWarning(ex, "File validation error (receipt) during E-Wallet order."); return Json(new { success = false, message = ex.Message }); }
                catch (Exception ex) { _logger.LogError(ex, "Error saving receipt file during E-Wallet order."); return Json(new { success = false, message = "حدث خطأ أثناء حفظ إيصال الدفع." }); }
                if (string.IsNullOrEmpty(receiptFilePath)) { return Json(new { success = false, message = "لم يتم حفظ إيصال الدفع بنجاح." }); }
            }
            else { return Json(new { success = false, message = "إيصال الدفع مطلوب." }); }

            var order = new Order
            {
                PaymentMethod = "E-Wallet",
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                CreatedAt = DateTime.UtcNow,
                ReceiptFileName = receiptFilePath,
                ShippingZoneId = request.ShippingZoneId, // Save ShippingZoneId
                ShippingFee = actualShippingFee,        // Save actual ShippingFee from DB
                SelectedShippingZone = shippingZone     // Assign the loaded zone
            };

            foreach (var itemVM in cartItemsVM)
            {
                var productFromDb = await _context.Products.FindAsync(itemVM.ProductId);
                if (productFromDb == null)
                {
                    if (!string.IsNullOrEmpty(receiptFilePath)) _fileService.DeleteFile(receiptFilePath);
                    return Json(new { success = false, message = $"المنتج '{itemVM.Title}' لم يعد متوفرًا." });
                }
                order.OrderItems.Add(new OrderItem { ProductId = productFromDb.Id, Title = productFromDb.TitleAr, Price = productFromDb.Price, Quantity = itemVM.Quantity, ImageUrl = productFromDb.ImageUrl });
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitInstapayOrder([FromForm] InstapayOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                return Json(new { success = false, message = "يرجى تصحيح الأخطاء التالية:", errors });
            }

            var shippingZone = await _context.ShippingZones
                                       .Include(sz => sz.ShippingCost)
                                       .FirstOrDefaultAsync(sz => sz.Id == request.ShippingZoneId && sz.IsActive && sz.ShippingCost != null);
            if (shippingZone == null || shippingZone.ShippingCost == null)
            {
                return Json(new { success = false, message = "منطقة الشحن المختارة غير صالحة أو غير متوفرة." });
            }
            decimal actualShippingFee = shippingZone.ShippingCost.Cost;


            List<CartItemViewModel> cartItemsVM;
            try { cartItemsVM = JsonSerializer.Deserialize<List<CartItemViewModel>>(request.CartItemsJson ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CartItemViewModel>(); }
            catch (JsonException ex) { _logger.LogError(ex, "JSON deserialization error in SubmitInstapayOrder for cart: {CartJson}", request.CartItemsJson); return Json(new { success = false, message = "حدث خطأ في قراءة بيانات السلة." }); }

            if (!cartItemsVM.Any()) return Json(new { success = false, message = "السلة فارغة." });

            string? receiptFilePath = null;
            if (request.Receipt != null && request.Receipt.Length > 0)
            {
                try { receiptFilePath = await _fileService.SaveFileAsync(request.Receipt, "receipts/instapay", new[] { ".jpg", ".jpeg", ".png", ".pdf" }); }
                catch (ArgumentException ex) { _logger.LogWarning(ex, "File validation error (receipt) during Instapay order."); return Json(new { success = false, message = ex.Message }); }
                catch (Exception ex) { _logger.LogError(ex, "Error saving receipt file during Instapay order."); return Json(new { success = false, message = "حدث خطأ أثناء حفظ إيصال الدفع." }); }
                if (string.IsNullOrEmpty(receiptFilePath)) { return Json(new { success = false, message = "لم يتم حفظ إيصال الدفع بنجاح." }); }
            }
            else { return Json(new { success = false, message = "إيصال الدفع مطلوب." }); }

            var order = new Order
            {
                PaymentMethod = "Instapay",
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                CreatedAt = DateTime.UtcNow,
                ReceiptFileName = receiptFilePath,
                ShippingZoneId = request.ShippingZoneId,
                ShippingFee = actualShippingFee,
                SelectedShippingZone = shippingZone
            };

            foreach (var itemVM in cartItemsVM)
            {
                var productFromDb = await _context.Products.FindAsync(itemVM.ProductId);
                if (productFromDb == null)
                {
                    if (!string.IsNullOrEmpty(receiptFilePath)) _fileService.DeleteFile(receiptFilePath);
                    return Json(new { success = false, message = $"المنتج '{itemVM.Title}' لم يعد متوفرًا." });
                }
                order.OrderItems.Add(new OrderItem { ProductId = productFromDb.Id, Title = productFromDb.TitleAr, Price = productFromDb.Price, Quantity = itemVM.Quantity, ImageUrl = productFromDb.ImageUrl });
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

            var shippingZone = await _context.ShippingZones
                                       .Include(sz => sz.ShippingCost)
                                       .FirstOrDefaultAsync(sz => sz.Id == request.ShippingZoneId && sz.IsActive && sz.ShippingCost != null);
            if (shippingZone == null || shippingZone.ShippingCost == null)
            {
                return Json(new { success = false, message = "منطقة الشحن المختارة غير صالحة أو غير متوفرة." });
            }
            decimal actualShippingFee = shippingZone.ShippingCost.Cost;

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

            string idImageFilePath = null;
           

            var order = new Order
            {
                PaymentMethod = "Cash",
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                IdImageFileName = idImageFilePath,
                CreatedAt = DateTime.UtcNow,
                ShippingZoneId = request.ShippingZoneId,
                ShippingFee = actualShippingFee,
                SelectedShippingZone = shippingZone
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
                order.OrderItems.Add(new OrderItem { ProductId = productFromDb.Id, Title = productFromDb.TitleAr, Price = productFromDb.Price, Quantity = itemVM.Quantity, ImageUrl = productFromDb.ImageUrl });
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

        // This method is for manual Visa confirmation, not Paymob redirect.
        // If Paymob is the only Visa method, this might need adjustment or removal.
        // For now, I'll add ShippingZoneId and ShippingFee handling similar to others.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitVisaOrder([FromForm] VisaOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "بيانات طلب الفيزا غير كاملة أو غير صحيحة.";
                // If this is called via AJAX, JSON response is better.
                // For now, keeping redirect to align with its original structure.
                var activeShippingZones = await _context.ShippingZones
                                             .Include(sz => sz.ShippingCost)
                                             .Where(sz => sz.IsActive && sz.ShippingCost != null && sz.ShippingCost.Cost >= 0)
                                             .OrderBy(sz => sz.NameAr)
                                             .Select(sz => new ShippingZoneViewModel
                                             {
                                                 Id = sz.Id,
                                                 NameAr = sz.NameAr,
                                                 NameEn = sz.NameEn,
                                                 Cost = sz.ShippingCost != null ? sz.ShippingCost.Cost : 0
                                             })
                                             .ToListAsync();
                ViewBag.ShippingZones = activeShippingZones;
                return View("Register", request); // Return to Register view with model state errors
            }

            var shippingZone = await _context.ShippingZones
                                       .Include(sz => sz.ShippingCost)
                                       .FirstOrDefaultAsync(sz => sz.Id == request.ShippingZoneId && sz.IsActive && sz.ShippingCost != null);
            if (shippingZone == null || shippingZone.ShippingCost == null)
            {
                TempData["ErrorMessage"] = "منطقة الشحن المختارة غير صالحة أو غير متوفرة.";
                var activeShippingZones = await _context.ShippingZones
                                            .Include(sz => sz.ShippingCost)
                                            .Where(sz => sz.IsActive && sz.ShippingCost != null && sz.ShippingCost.Cost >= 0)
                                            .OrderBy(sz => sz.NameAr)
                                            .Select(sz => new ShippingZoneViewModel
                                            {
                                                Id = sz.Id,
                                                NameAr = sz.NameAr,
                                                NameEn = sz.NameEn,
                                                Cost = sz.ShippingCost != null ? sz.ShippingCost.Cost : 0
                                            })
                                            .ToListAsync();
                ViewBag.ShippingZones = activeShippingZones;
                return View("Register", request);
            }
            decimal actualShippingFee = shippingZone.ShippingCost.Cost;

            List<CartItemViewModel> cartItemsVM;
            try { cartItemsVM = JsonSerializer.Deserialize<List<CartItemViewModel>>(request.CartItemsJson ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CartItemViewModel>(); }
            catch (JsonException ex) { _logger.LogError(ex, "JSON Error Visa Order: {CartJson}", request.CartItemsJson); TempData["ErrorMessage"] = "خطأ في بيانات السلة لطلب الفيزا."; return RedirectToAction("Register"); }

            if (!cartItemsVM.Any()) { TempData["ErrorMessage"] = "السلة فارغة لطلب الفيزا."; return RedirectToAction("Register"); }

            string? confirmationFilePath = null;
            if (request.ConfirmationFile != null && request.ConfirmationFile.Length > 0)
            {
                try { confirmationFilePath = await _fileService.SaveFileAsync(request.ConfirmationFile, "visa_confirmations", new[] { ".jpg", ".jpeg", ".png", ".pdf" }); }
                catch (ArgumentException ex) { _logger.LogWarning(ex, "File validation error (Visa confirmation) during manual Visa order."); TempData["ErrorMessage"] = ex.Message; return RedirectToAction("Register"); }
                catch (Exception ex) { _logger.LogError(ex, "Error saving Visa confirmation file."); TempData["ErrorMessage"] = "حدث خطأ أثناء حفظ ملف تأكيد الفيزا."; return RedirectToAction("Register"); }
                if (string.IsNullOrEmpty(confirmationFilePath)) { TempData["ErrorMessage"] = "لم يتم حفظ ملف تأكيد الفيزا بنجاح."; return RedirectToAction("Register"); }
            }
            // ConfirmationFile is optional in VisaOrderRequest, so no error if it's null/empty.

            var order = new Order
            {
                PaymentMethod = "Visa (Manual)", // Or "Visa (Paymob)" if this action is for Paymob redirect later
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                ConfirmationFileName = confirmationFilePath, // For manual visa
                CreatedAt = DateTime.UtcNow,
                ShippingZoneId = request.ShippingZoneId,
                ShippingFee = actualShippingFee,
                SelectedShippingZone = shippingZone
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
                order.OrderItems.Add(new OrderItem { ProductId = productFromDb.Id, Title = productFromDb.TitleAr, Price = productFromDb.Price, Quantity = itemVM.Quantity, ImageUrl = productFromDb.ImageUrl });
            }

            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
                // If this is for Paymob, the redirect should be to Paymob's iframe, not OrderConfirmation directly.
                // For now, keeping original flow for "Visa (Manual)"
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
            var order = await _context.Orders
                                .Include(o => o.OrderItems)
                                .Include(o => o.SelectedShippingZone) // Eager load shipping zone
                                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) { TempData["ErrorMessage"] = "لم يتم العثور على الطلب."; return RedirectToAction("Index", "Home"); }

            // Clear cart from localStorage after successful order placement
            // This is better done on the client-side upon redirect to confirmation page.
            // HttpContext.Response.Cookies.Append("clearCart", "true"); // Example signal to client

            return View(order);
        }
    }
    // --- DTOs (Data Transfer Objects) / ViewModels for CartController ---

    // ViewModel for ShippingZone to pass to the View (includes cost)
    public class ShippingZoneViewModel
        {
            public int Id { get; set; }
            public string? NameAr { get; set; }
            public string? NameEn { get; set; }
            public decimal Cost { get; set; }
        }


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

            [Required(ErrorMessage = "منطقة الشحن مطلوبة")]
            [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار منطقة شحن صالحة.")]
            public int ShippingZoneId { get; set; }
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

            public IFormFile? IdImage { get; set; }

            [Required(ErrorMessage = "بيانات السلة مطلوبة")]
            public string? CartItemsJson { get; set; }

            [Required(ErrorMessage = "منطقة الشحن مطلوبة")]
            [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار منطقة شحن صالحة.")]
            public int ShippingZoneId { get; set; }
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

            [Required(ErrorMessage = "منطقة الشحن مطلوبة")]
            [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار منطقة شحن صالحة.")]
            public int ShippingZoneId { get; set; }
        }

        public class VisaOrderRequest // This is for Manual Visa, not Paymob redirect
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

            [Required(ErrorMessage = "منطقة الشحن مطلوبة")]
            [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار منطقة شحن صالحة.")]
            public int ShippingZoneId { get; set; }
        }

        // ViewModel for items coming from client-side cart (localStorage)
        public class CartItemViewModel
        {
            [Required(ErrorMessage = "معرف المنتج مطلوب في السلة")]
            [Range(1, int.MaxValue, ErrorMessage = "معرف المنتج غير صالح في السلة.")]
            public int ProductId { get; set; } // Ensure this matches the casing from JS (productId)

            public string? Title { get; set; } // Ensure this matches the casing from JS (title)

            public decimal Price { get; set; } // Ensure this matches the casing from JS (price)

            [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون 1 على الأقل")]
            public int Quantity { get; set; } // Ensure this matches the casing from JS (quantity)

            public string? ImageUrl { get; set; } // Ensure this matches the casing from JS (imageUrl)
        }
        public class PickupOrderViewModel
        {
            [Required(ErrorMessage = "الاسم الكامل مطلوب.")]
            public string FullName { get; set; }

            [Required(ErrorMessage = "رقم الهاتف مطلوب.")]
            public string PhoneNumber { get; set; }

            public string PaymentMethod { get; set; }
            public decimal TotalPrice { get; set; }
            public List<CartItemViewModel> CartItems { get; set; }
        }


    
}