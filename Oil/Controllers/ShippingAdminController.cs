using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oil.Data;
using Oil.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Required for HttpContext.Session

namespace Oil.Controllers
{
    public class ShippingAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ShippingAdminController> _logger;

        public ShippingAdminController(ApplicationDbContext context, ILogger<ShippingAdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private bool IsLoggedIn() => !string.IsNullOrEmpty(HttpContext.Session.GetString("Username"));

        // GET: ShippingAdmin
        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");

            var shippingZones = await _context.ShippingZones
                                              .Include(sz => sz.ShippingCost)
                                              .OrderBy(sz => sz.NameAr)
                                              .ToListAsync();
            return View(shippingZones);
        }

        // GET: ShippingAdmin/Create
        public IActionResult Create()
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");
            return View(new ShippingZone { IsActive = true }); // Default IsActive to true
        }

        // POST: ShippingAdmin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("NameAr,NameEn,IsActive,ShippingCost")] ShippingZone shippingZone)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");

            // Remove ModelState errors for navigation properties if they are not meant to be bound directly
            ModelState.Remove("ShippingCost.ShippingZone"); // ShippingZone is navigation property in ShippingCost

            if (ModelState.IsValid)
            {
                try
                {
                    // Ensure ShippingCost is associated if provided
                    if (shippingZone.ShippingCost != null)
                    {
                        // The ShippingZoneId in ShippingCost will be set automatically by EF Core
                        // when the ShippingZone (parent) is added and has ShippingCost as a child.
                        // Or, if ShippingCost is created separately, ensure its ShippingZoneId is 0
                        // and it will be linked correctly.
                        // For this binding, EF core should handle it if ShippingCost is part of ShippingZone model.
                    }

                    _context.Add(shippingZone);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تمت إضافة منطقة الشحن بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error creating shipping zone. Check for duplicate names.");
                    // Check for unique constraint violation (NameAr or NameEn)
                    if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE KEY constraint"))
                    {
                        ModelState.AddModelError("", "اسم منطقة الشحن (عربي أو إنجليزي) موجود بالفعل. يرجى استخدام اسم آخر.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "حدث خطأ أثناء حفظ منطقة الشحن. يرجى المحاولة مرة أخرى.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error creating shipping zone.");
                    ModelState.AddModelError("", "حدث خطأ غير متوقع.");
                }
            }
            // If we got this far, something failed, redisplay form
            // Ensure ShippingCost object is available if validation fails and view is re-rendered
            if (shippingZone.ShippingCost == null)
            {
                shippingZone.ShippingCost = new ShippingCost();
            }
            return View(shippingZone);
        }


        // GET: ShippingAdmin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");

            if (id == null)
            {
                return NotFound();
            }

            var shippingZone = await _context.ShippingZones
                                             .Include(sz => sz.ShippingCost)
                                             .FirstOrDefaultAsync(sz => sz.Id == id);
            if (shippingZone == null)
            {
                return NotFound();
            }
            // Ensure ShippingCost is not null for the view
            if (shippingZone.ShippingCost == null)
            {
                shippingZone.ShippingCost = new ShippingCost { ShippingZoneId = shippingZone.Id };
            }
            return View(shippingZone);
        }

        // POST: ShippingAdmin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,NameAr,NameEn,IsActive,ShippingCost")] ShippingZone shippingZone)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");

            if (id != shippingZone.Id)
            {
                return NotFound();
            }

            ModelState.Remove("ShippingCost.ShippingZone");


            if (ModelState.IsValid)
            {
                try
                {
                    var existingZone = await _context.ShippingZones.Include(s => s.ShippingCost).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
                    if (existingZone == null) return NotFound();

                    // Update the ShippingZone properties
                    _context.Entry(shippingZone).State = EntityState.Modified;

                    // Handle ShippingCost
                    if (shippingZone.ShippingCost != null)
                    {
                        shippingZone.ShippingCost.ShippingZoneId = shippingZone.Id; // Ensure FK is set
                        if (existingZone.ShippingCost != null)
                        {
                            _context.Entry(shippingZone.ShippingCost).State = EntityState.Modified;
                        }
                        else
                        {
                            _context.ShippingCosts.Add(shippingZone.ShippingCost);
                        }
                    }
                    else if (existingZone.ShippingCost != null) // If new cost is null but old one existed, remove old one
                    {
                        _context.ShippingCosts.Remove(existingZone.ShippingCost);
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تعديل منطقة الشحن بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShippingZoneExists(shippingZone.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        _logger.LogError("Concurrency error editing shipping zone ID {ShippingZoneId}", shippingZone.Id);
                        ModelState.AddModelError("", "تم تعديل البيانات من قبل مستخدم آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.");
                    }
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error updating shipping zone ID {ShippingZoneId}. Check for duplicate names.", shippingZone.Id);
                    if (ex.InnerException != null && ex.InnerException.Message.Contains("UNIQUE KEY constraint"))
                    {
                        ModelState.AddModelError("NameAr", "اسم منطقة الشحن (عربي أو إنجليزي) موجود بالفعل. يرجى استخدام اسم آخر.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "حدث خطأ أثناء حفظ التعديلات. يرجى المحاولة مرة أخرى.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error editing shipping zone ID {ShippingZoneId}", shippingZone.Id);
                    ModelState.AddModelError("", "حدث خطأ غير متوقع.");
                }
            }
            // If we got this far, something failed, redisplay form
            if (shippingZone.ShippingCost == null) // Ensure ShippingCost is not null for the view if validation fails
            {
                shippingZone.ShippingCost = new ShippingCost { ShippingZoneId = shippingZone.Id };
            }
            return View(shippingZone);
        }


        // POST: ShippingAdmin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Index", "Login");

            var shippingZone = await _context.ShippingZones.Include(sz => sz.ShippingCost).FirstOrDefaultAsync(sz => sz.Id == id);
            if (shippingZone == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على منطقة الشحن.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Orders might be referencing this ShippingZoneId.
                // Check if any order is using this shipping zone.
                bool isZoneInUse = await _context.Orders.AnyAsync(o => o.ShippingZoneId == id);
                if (isZoneInUse)
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف منطقة الشحن هذه لأنها مستخدمة في بعض الطلبات. يمكنك تعطيلها بدلاً من ذلك.";
                    return RedirectToAction(nameof(Index));
                }

                if (shippingZone.ShippingCost != null)
                {
                    _context.ShippingCosts.Remove(shippingZone.ShippingCost);
                }
                _context.ShippingZones.Remove(shippingZone);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف منطقة الشحن بنجاح.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shipping zone ID {ShippingZoneId}", id);
                TempData["ErrorMessage"] = "حدث خطأ أثناء محاولة الحذف.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ShippingZoneExists(int id)
        {
            return _context.ShippingZones.Any(e => e.Id == id);
        }
    }
}
