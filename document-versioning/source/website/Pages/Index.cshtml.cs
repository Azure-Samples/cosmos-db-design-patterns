using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Versioning;

namespace website.Pages;

public class IndexModel : PageModel
{

    public List<VersionedOrder> SubmittedOrders = new List<VersionedOrder>();
    public List<VersionedOrder> FulfilledOrders = new List<VersionedOrder>();
    public List<VersionedOrder> DeliveredOrders = new List<VersionedOrder>();
    public List<VersionedOrder> CancelledOrders = new List<VersionedOrder>();
    private OrderHelper helper = new OrderHelper();

    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public async Task OnGet()
    {
        await GetOrders();
    }

    public async Task<IActionResult> OnPost(){
        int numberToCreate = Convert.ToInt32(Request.Form["DocCount"]);
        for (int counter = 0; counter < numberToCreate; counter++)
        {
            Order newOrder = helper.GenerateOrder();
            await helper.SaveOrder(newOrder);
        }
        await GetOrders();
        return Page();
    }

    private async Task GetOrders()
    {
        List<VersionedOrder> orders = (await helper.RetrieveAllOrdersAsync()).ToList();
        SubmittedOrders = orders.Where(order => order.Status == "Submitted").ToList();
        FulfilledOrders = orders.Where(order => order.Status == "Fulfilled").ToList();
        DeliveredOrders = orders.Where(order => order.Status == "Delivered").ToList();
        CancelledOrders = orders.Where(order => order.Status == "Cancelled").ToList();
    }
}
