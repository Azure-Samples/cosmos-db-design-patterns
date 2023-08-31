using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Versioning;

namespace website.Pages;

public class IndexModel : PageModel
{
    public List<Cart> Carts = new();
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        Carts = CartHelper.RetrieveAllCartsAsync().Result.ToList();        
    }
}
