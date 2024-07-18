using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Versioning.Models;
using Versioning.Services;

namespace website.Pages;

public class IndexModel : PageModel
{
    public List<Cart> Carts;
    private readonly ILogger<IndexModel> _logger;
    private readonly CartService _cartService;

    public IndexModel(ILogger<IndexModel> logger, CartService cartService)
    {
        _logger = logger;
        _cartService = cartService;
        Carts = new List<Cart>();
    }

    public void OnGet()
    {
        Carts = _cartService.RetrieveAllCartsAsync().Result.ToList();        
    }
}
