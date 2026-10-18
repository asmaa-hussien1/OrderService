using Microsoft.AspNetCore.Mvc;
using OrderService.Models;

namespace OrderService.Controllers
{
    [Route("api/orders")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        // قائمة مؤقتة في الذاكرة لتخزين الطلبات
        private static readonly List<Order> _orders = new List<Order>();
        private static int _nextId = 1;

        private readonly HttpClient _httpClient;

        // بنحقن الـ HttpClient عشان نقدر نكلم خدمة أحمد (Inventory Service)
        public OrdersController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // 1. عرض كل الطلبات: GET /api/orders
        [HttpGet]
        public ActionResult<IEnumerable<Order>> GetAllOrders()
        {
            return Ok(_orders);
        }

        // 2. عرض طلب محدد بالـ ID: GET /api/orders/{id}
        [HttpGet("{id}")]
        public ActionResult<Order> GetOrderById(int id)
        {
            var order = _orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
            {
                return NotFound(new { message = $"Order with ID {id} not found." });
            }
            return Ok(order);
        }

        // 3. إنشاء طلب شراء جديد: POST /api/orders
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto request)
        {
            // 1. التحقق البسيط من البيانات المرسلة
            if (request.Quantity <= 0 || request.ProductId <= 0)
            {
                return BadRequest(new { error = new { code = "INVALID_REQUEST", message = "Invalid product ID or quantity." } });
            }

            // 2. إرسال طلب حجز الكمية إلى Inventory Service الخاصة بأحمد
            // Base URL الخاص بأحمد: http://localhost:8081
            string inventoryUrl = "http://localhost:8081/api/inventory/reservations";

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(inventoryUrl, request);
            }
            catch (Exception)
            {
                // لو خدمة المخزون مقفولة أو مش متاحة
                return StatusCode(503, new { error = new { code = "SERVICE_UNAVAILABLE", message = "Inventory service is not available." } });
            }

            // 3. لو الحجز فشل عند أحمد (مثلاً 409 Conflict أو 404 Not Found)
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, errorContent);
            }

            // 4. لو الحجز نجح (201 Created)، نقرا تفاصيل الحجز ونرجعها مع الطلب المؤكد
            var reservationResult = await response.Content.ReadFromJsonAsync<ReservationResponseDto>();

            var newOrder = new Order
            {
                Id = _nextId++,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                Status = "Confirmed",
                ReservationId = reservationResult?.ReservationId ?? 0
            };

            _orders.Add(newOrder);

            // 5. إرجاع النتيجة للعميل 201 Created
            return StatusCode(201, newOrder);
        }
    }

    // DTOs (Data Transfer Objects) الخاصة بالـ Request والـ Response لتنظيم البيانات
    public class CreateOrderDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class ReservationResponseDto
    {
        public int ReservationId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public int RemainingQuantity { get; set; }
    }
}