using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using res_menu.Models;
using res_menu.Services;

namespace res_menu.Pages.Restaurant;

[Authorize]
public class OrderManagementModel : PageModel
{
    private readonly IOrderService _orderService;
    private readonly IRestaurantService _restaurantService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<OrderManagementModel> _logger;

    public OrderManagementModel(
        IOrderService orderService,
        IRestaurantService restaurantService,
        UserManager<IdentityUser> userManager,
        ILogger<OrderManagementModel> logger)
    {
        _orderService = orderService;
        _restaurantService = restaurantService;
        _userManager = userManager;
        _logger = logger;
    }    public res_menu.Models.Restaurant? Restaurant { get; set; }
    public List<res_menu.Models.Order> Orders { get; set; } = new();
    public OrderStatus? SelectedStatus { get; set; }

    public async Task<IActionResult> OnGetAsync(OrderStatus? status = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        Restaurant = await _restaurantService.GetRestaurantByOwnerIdAsync(userId);
        if (Restaurant == null)
        {
            TempData["ErrorMessage"] = "You don't have a restaurant registered. Please create one first.";
            return RedirectToPage("CreateRestaurant");
        }

        SelectedStatus = status;
        Orders = await _orderService.GetOrdersByRestaurantAndStatusAsync(Restaurant.Id, status);

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, OrderStatus status)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Verify the order belongs to the user's restaurant
            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }

            Restaurant = await _restaurantService.GetRestaurantByOwnerIdAsync(userId);
            if (Restaurant == null || order.RestaurantId != Restaurant.Id)
            {
                return Forbid();
            }

            await _orderService.UpdateOrderStatusAsync(orderId, status);
            TempData["SuccessMessage"] = "Order status updated successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status");
            TempData["ErrorMessage"] = "An error occurred while updating the order status.";
        }

        return RedirectToPage();
    }
}
