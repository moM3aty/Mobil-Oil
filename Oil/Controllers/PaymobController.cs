using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oil.Data;
using Oil.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class PaymobController : Controller
{
    private readonly HttpClient _client;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymobController> _logger;
    private readonly string _paymobApiKey;
    private readonly string _paymobIntegrationId;
    private readonly string _paymobIframeId;

    public PaymobController(ApplicationDbContext context, IConfiguration configuration, ILogger<PaymobController> logger)
    {
        _client = new HttpClient();
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _paymobApiKey = _configuration["PaymobSettings:ApiKey"] ?? throw new ArgumentNullException("PaymobSettings:ApiKey not configured");
        _paymobIntegrationId = _configuration["PaymobSettings:IntegrationId"] ?? throw new ArgumentNullException("PaymobSettings:IntegrationId not configured");
        _paymobIframeId = _configuration["PaymobSettings:IframeId"] ?? throw new ArgumentNullException("PaymobSettings:IframeId not configured");
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var payload = new { api_key = _paymobApiKey };
        var response = await _client.PostAsync(
            "https://accept.paymob.com/api/auth/tokens",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(content);
        return json.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("Auth token not found in Paymob response.");
    }

    private async Task<int> CreatePaymobOrderAsync(string authToken, decimal amountInEGP, List<OrderItemDto>? clientItems, string merchantOrderId)
    {
        var amountCents = (int)(amountInEGP * 100);

        List<object> paymobItems = clientItems? // Use clientItems for Paymob's display
            .Select(item => (object)new
            {
                name = item.Title ?? "N/A", // Title from client for Paymob display
                amount_cents = (int)((item.Price * item.Quantity) * 100),
                description = item.Title ?? "Item",
                quantity = item.Quantity
            })
            .ToList() ?? new List<object>();

        var payload = new
        {
            auth_token = authToken,
            delivery_needed = false,
            amount_cents = amountCents,
            currency = "EGP",
            merchant_order_id = merchantOrderId,
            items = paymobItems
        };

        var response = await _client.PostAsync(
            "https://accept.paymob.com/api/ecommerce/orders",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(content);
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<string> GetPaymentKeyAsync(string authToken, decimal amountInEGP, int paymobOrderId, PayRequest billingInfo)
    {
        var amountCents = (int)(amountInEGP * 100);
        var billingData = new
        {
            apartment = "NA",
            email = billingInfo.Email ?? "customer@example.com",
            floor = "NA",
            first_name = billingInfo.Name?.Split(' ').FirstOrDefault() ?? "Customer",
            last_name = billingInfo.Name?.Split(' ').LastOrDefault() ?? "Name",
            phone_number = billingInfo.Phone ?? "0000000000",
            city = billingInfo.City ?? "Cairo",
            country = "EG",
            street = billingInfo.Address ?? "NA",
            building = "NA",
            shipping_method = "NA",
            postal_code = billingInfo.PostalCode ?? "NA",
            state = billingInfo.State ?? "Cairo"
        };

        var payload = new
        {
            auth_token = authToken,
            amount_cents = amountCents,
            expiration = 3600,
            order_id = paymobOrderId,
            billing_data = billingData,
            currency = "EGP",
            integration_id = int.Parse(_paymobIntegrationId),
            lock_order_when_paid = "false"
        };

        var response = await _client.PostAsync(
            "https://accept.paymob.com/api/acceptance/payment_keys",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(content);
        return json.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("Payment key not found in Paymob response.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay([FromBody] PayRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning("Paymob Pay action ModelState invalid: {Errors}", string.Join(", ", errors));
            return BadRequest(new { success = false, message = "بيانات الطلب غير صالحة.", errors });
        }

        if (request.Amount <= 0) // request.Amount is grand total from client
        {
            _logger.LogWarning("Paymob Pay action invalid request amount: {Amount}", request.Amount);
            return BadRequest(new { success = false, message = "المبلغ الإجمالي للطلب غير صالح." });
        }
        if (request.Items == null || !request.Items.Any())
        {
            _logger.LogWarning("Paymob Pay action cart is empty.");
            return BadRequest(new { success = false, message = "السلة فارغة." });
        }

        var shippingZoneFromDb = await _context.ShippingZones
            .Include(sz => sz.ShippingCost)
            .AsNoTracking()
            .FirstOrDefaultAsync(sz => sz.Id == request.ShippingZoneId && sz.IsActive && sz.ShippingCost != null);

        if (shippingZoneFromDb == null || shippingZoneFromDb.ShippingCost == null)
        {
            _logger.LogWarning("Paymob Pay action invalid shipping zone ID: {ShippingZoneId}", request.ShippingZoneId);
            return BadRequest(new { success = false, message = "منطقة الشحن المختارة غير صالحة أو غير متوفرة." });
        }
        decimal actualShippingFee = shippingZoneFromDb.ShippingCost.Cost;

        decimal serverCalculatedProductsTotal = 0;
        var orderItemsForDb = new List<OrderItem>();
        string currentRequestLang = Request.Cookies["Language"] ?? "ar"; // Get language for titles

        foreach (var itemDto in request.Items)
        {
            if (itemDto.ProductId <= 0 || itemDto.Quantity <= 0 || itemDto.Price < 0) // Price can be 0 for free items
            {
                _logger.LogWarning("Paymob Pay action: Invalid data in ItemDto. ProductId: {ProductId}, Quantity: {Quantity}, Price: {Price}", itemDto.ProductId, itemDto.Quantity, itemDto.Price);
                return BadRequest(new { success = false, message = $"بيانات غير صالحة لأحد المنتجات في السلة: {itemDto.Title ?? "منتج غير محدد"}" });
            }

            var productFromDb = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == itemDto.ProductId);
            if (productFromDb == null)
            {
                _logger.LogWarning("Paymob Pay action: ProductId {ProductId} from DTO not found in DB. Client Title: {ClientTitle}", itemDto.ProductId, itemDto.Title);
                return BadRequest(new { success = false, message = $"منتج '{itemDto.Title ?? "غير معروف"}' (ID: {itemDto.ProductId}) غير موجود. يرجى تحديث سلتك." });
            }

            orderItemsForDb.Add(new OrderItem
            {
                ProductId = productFromDb.Id,
                Title = (currentRequestLang == "ar" && !string.IsNullOrEmpty(productFromDb.TitleAr)) ? productFromDb.TitleAr : productFromDb.TitleEn, // Use DB title
                Price = productFromDb.Price,       // CRITICAL: Use price from DB
                Quantity = itemDto.Quantity,
                ImageUrl = productFromDb.ImageUrl
            });
            serverCalculatedProductsTotal += (productFromDb.Price * itemDto.Quantity);
        }

        decimal serverCalculatedGrandTotal = serverCalculatedProductsTotal + actualShippingFee;

        if (Math.Abs(request.Amount - serverCalculatedGrandTotal) > 0.01m)
        {
            _logger.LogWarning($"Paymob amount mismatch. Client GrandTotal: {request.Amount}, Server Calculated GrandTotal (Items {serverCalculatedProductsTotal} + Shipping {actualShippingFee}): {serverCalculatedGrandTotal}");
            return BadRequest(new { success = false, message = "إجمالي المبلغ غير متطابق مع محتويات السلة وتكلفة الشحن. يرجى تحديث الصفحة والمحاولة مرة أخرى." });
        }

        var localOrder = new Order
        {
            PaymentMethod = "Visa (Paymob)",
            Name = request.Name!,
            Phone = request.Phone!,
            Address = request.Address!,
            CreatedAt = DateTime.UtcNow,
            ShippingZoneId = request.ShippingZoneId,
            ShippingFee = actualShippingFee,
            OrderItems = orderItemsForDb // Assign the validated and DB-sourced items
        };

        try
        {
            _context.Orders.Add(localOrder);
            await _context.SaveChangesAsync();

            string authToken = await GetAuthTokenAsync();
            // Use the validated serverCalculatedGrandTotal for Paymob API calls
            int paymobOrderId = await CreatePaymobOrderAsync(authToken, serverCalculatedGrandTotal, request.Items, localOrder.Id.ToString());

            localOrder.PaymobOrderId = paymobOrderId;
            await _context.SaveChangesAsync();

            string paymentKey = await GetPaymentKeyAsync(authToken, serverCalculatedGrandTotal, paymobOrderId, request);
            string paymentUrl = $"https://accept.paymob.com/api/acceptance/iframes/{_paymobIframeId}?payment_token={paymentKey}";

            _logger.LogInformation("Paymob payment initiated for order ID {LocalOrderId}, Paymob Order ID {PaymobOrderId}. Redirecting to: {PaymentUrl}", localOrder.Id, paymobOrderId, paymentUrl);
            return Json(new { success = true, url = paymentUrl, orderId = localOrder.Id });
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "Paymob API HTTP Error during Pay action for potential order by {Name}", request.Name);
            return StatusCode(500, new { success = false, message = $"حدث خطأ أثناء الاتصال بخدمة الدفع. ({httpEx.StatusCode})" });
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Paymob API JSON Error during Pay action for potential order by {Name}", request.Name);
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء معالجة استجابة خدمة الدفع." });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database update error during Pay action for {Name}. InnerEx: {InnerMessage}", request.Name, dbEx.InnerException?.Message);
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء حفظ الطلب في قاعدة البيانات. التفاصيل: " + dbEx.InnerException?.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal server error during Pay action for {Name}", request.Name);
            return StatusCode(500, new { success = false, message = $"حدث خطأ داخلي: {ex.Message}" });
        }
    }
}

// --- DTOs (Data Transfer Objects) / ViewModels for PaymobController ---
// (These should be defined below the class or in a separate file)

public class OrderItemDto
{
    [Required(ErrorMessage = "معرف المنتج مطلوب في تفاصيل السلعة")]
    public int ProductId { get; set; }

    public string? Title { get; set; } // Title from client, used for Paymob itemization

    [Range(0.00, double.MaxValue, ErrorMessage = "سعر السلعة لا يمكن أن يكون سالباً")] // Allow 0 for potentially free items
    public decimal Price { get; set; } // Price from client, used for Paymob itemization

    [Range(1, int.MaxValue, ErrorMessage = "كمية السلعة يجب أن تكون 1 على الأقل")]
    public int Quantity { get; set; }

    public string? ImageUrl { get; set; }
}

public class PayRequest
{
    [Required(ErrorMessage = "الاسم مطلوب")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "رقم الهاتف مطلوب")]
    [Phone(ErrorMessage = "رقم هاتف غير صالح")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "العنوان مطلوب")]
    public string? Address { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
    public decimal Amount { get; set; } // GRAND TOTAL (Products + Shipping) from client

    [Required(ErrorMessage = "تفاصيل السلة مطلوبة")]
    [MinLength(1, ErrorMessage = "يجب أن تحتوي السلة على منتج واحد على الأقل.")]
    public List<OrderItemDto>? Items { get; set; }

    [EmailAddress(ErrorMessage = "بريد إلكتروني غير صالح")]
    public string? Email { get; set; } = "customer@example.com";

    public string? City { get; set; } = "Cairo";
    public string? PostalCode { get; set; } = "NA";
    public string? State { get; set; } = "Cairo";

    [Required(ErrorMessage = "منطقة الشحن مطلوبة")]
    [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار منطقة شحن صالحة.")]
    public int ShippingZoneId { get; set; }
}
