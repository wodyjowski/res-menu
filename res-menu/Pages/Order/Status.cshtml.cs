using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using res_menu.Models;
using res_menu.Services;

namespace res_menu.Pages.Order;

[AllowAnonymous]
public class StatusModel : PageModel
{
    private readonly IOrderService _orderService;
    private readonly ILogger<StatusModel> _logger;

    public StatusModel(IOrderService orderService, ILogger<StatusModel> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    public res_menu.Models.Order? Order { get; set; }

    public async Task<IActionResult> OnGetAsync(string? customerOrderId)
    {
        // If no customerOrderId provided, try to get from cookie
        if (string.IsNullOrEmpty(customerOrderId))
        {
            customerOrderId = Request.Cookies["CustomerOrderId"];
        }

        if (string.IsNullOrEmpty(customerOrderId))
        {
            return NotFound("No order found. Please check your order confirmation link.");
        }

        Order = await _orderService.GetOrderByCustomerOrderIdAsync(customerOrderId);

        if (Order == null)
        {
            return NotFound("Order not found.");
        }

        return Page();
    }
}
