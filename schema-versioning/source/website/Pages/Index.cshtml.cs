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

    public async Task OnGet()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddAsync(bool versioned)
    {
        await _cartService.AddCartAsync(versioned);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        await _cartService.ClearAllAsync();
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Carts = (await _cartService.RetrieveAllCartsAsync()).ToList();
    }
}