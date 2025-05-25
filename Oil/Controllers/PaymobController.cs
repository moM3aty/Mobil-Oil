// PaymobController.cs - Continues to use IConfiguration
// Ensure the CreatePaymobOrderAsync method uses List<object> for paymobItems as corrected previously.
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

public class PaymobController : Controller
{
    private readonly HttpClient _client;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly string _paymobApiKey;
    private readonly string _paymobIntegrationId;
    private readonly string _paymobIframeId;

    public PaymobController(ApplicationDbContext context, IConfiguration configuration)
    {
        _client = new HttpClient();
        _context = context;
        _configuration = configuration;

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

    private async Task<int> CreatePaymobOrderAsync(string authToken, decimal amountInEGP, List<OrderItemDto>? items, string merchantOrderId)
    {
        var amountCents = (int)(amountInEGP * 100);

        List<object> paymobItems = items?
            .Select(item => (object)new
            {
                name = item.Title ?? "N/A",
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
    public async Task<IActionResult> Pay([FromBody] PayRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
            return BadRequest(new { success = false, message = "بيانات الطلب غير صالحة.", errors });
        }

        if (request.Amount <= 0)
            return BadRequest(new { success = false, message = "المبلغ غير صالح." });

        try
        {
            var localOrder = new Order
            {
                PaymentMethod = "Visa (Paymob)",
                Name = request.Name!,
                Phone = request.Phone!,
                Address = request.Address!,
                CreatedAt = DateTime.UtcNow,
            };

            if (request.Items != null && request.Items.Any())
            {
                foreach (var itemDto in request.Items)
                {
                    localOrder.OrderItems.Add(new OrderItem
                    {
                        Title = itemDto.Title ?? "منتج غير مسمى",
                        Price = itemDto.Price,
                        Quantity = itemDto.Quantity,
                        ImageUrl = itemDto.ImageUrl
                    });
                }
            }
            else
            {
                return BadRequest(new { success = false, message = "السلة فارغة." });
            }

            _context.Orders.Add(localOrder);
            await _context.SaveChangesAsync();

            string authToken = await GetAuthTokenAsync();
            int paymobOrderId = await CreatePaymobOrderAsync(authToken, request.Amount, request.Items, localOrder.Id.ToString());

            localOrder.PaymobOrderId = paymobOrderId;
            _context.Orders.Update(localOrder);
            await _context.SaveChangesAsync();

            string paymentKey = await GetPaymentKeyAsync(authToken, request.Amount, paymobOrderId, request);

            string paymentUrl = $"https://accept.paymob.com/api/acceptance/iframes/{_paymobIframeId}?payment_token={paymentKey}";

            return Json(new { success = true, url = paymentUrl, orderId = localOrder.Id });
        }
        catch (HttpRequestException httpEx)
        {
            Console.Error.WriteLine($"Paymob API HTTP Error: {httpEx.StatusCode} - {httpEx.Message}");
            return StatusCode(500, new { success = false, message = $"حدث خطأ أثناء الاتصال بخدمة الدفع. ({httpEx.StatusCode})" });
        }
        catch (JsonException jsonEx)
        {
            Console.Error.WriteLine($"Paymob API JSON Error: {jsonEx.Message}");
            return StatusCode(500, new { success = false, message = "حدث خطأ أثناء معالجة استجابة خدمة الدفع." });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Internal server error during Pay: {ex.Message}\nStackTrace: {ex.StackTrace}");
            return StatusCode(500, new { success = false, message = $"حدث خطأ داخلي: {ex.Message}" });
        }
    }
}

// PayRequest and OrderItemDto remain the same as previous version.
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
    public decimal Amount { get; set; }
    public List<OrderItemDto>? Items { get; set; }
    [EmailAddress(ErrorMessage = "بريد إلكتروني غير صالح")]
    public string? Email { get; set; } = "customer@example.com";
    public string? City { get; set; } = "Cairo";
    public string? PostalCode { get; set; } = "NA";
    public string? State { get; set; } = "Cairo";
}
public class OrderItemDto
{
    public string? Title { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }
}