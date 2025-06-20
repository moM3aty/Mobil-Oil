// LoginController.cs - Using BCryptAuth alias
using Oil.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oil.Data;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using BCryptAuth = BCrypt.Net.BCrypt;

namespace Oil.Controllers
{
    public class LoginController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoginController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (IsLoggedIn())
                return RedirectToAction("Dashboard", "Login");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == model.Username);

                if (user != null && BCryptAuth.Verify(model.Password, user.PasswordHash))
                {
                    HttpContext.Session.SetString("Username", user.Username);
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    return RedirectToAction("Dashboard", "Login");
                }

                ModelState.AddModelError("", "اسم المستخدم أو كلمة المرور غير صحيحة.");
            }
            return View(model);
        }

        public string HashPassword(string password)
        {
            return BCryptAuth.HashPassword(password);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Login");
        }

        public IActionResult Dashboard()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index");

            return View();
        }

        public async Task<IActionResult> Orders()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Login");

            var orders = await _context.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync();
            return View(orders);
        }

        public async Task<IActionResult> OrderItems(int orderId)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Login");

            var order = await _context.Orders
                                      .Include(o => o.OrderItems).Include(o => o.SelectedShippingZone)
                                      .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "الأوردر غير موجود.";
                return RedirectToAction("Orders");
            }

            return View(order);
        }
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();           
            return RedirectToAction(nameof(Orders));
        }
        private bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("Username"));
        }
    }
}
