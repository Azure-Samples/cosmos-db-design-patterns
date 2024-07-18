using Microsoft.AspNetCore.Mvc;
using Services;

public class StatusController : Controller {

    //private OrderHelper helper = new OrderHelper();

    private readonly OrderHelper _helper;

    public StatusController(OrderHelper helper)
    {
        _helper = helper;
    }

    [HttpGet("Cancel/{orderId}/{customerId}")]
    public async Task<IActionResult> Cancel(string orderId, int customerId){
        var versionedDocument = await _helper.RetrieveOrderAsync(orderId, customerId);
        _helper.CancelOrder(versionedDocument);
        await _helper.SaveVersionedOrder(versionedDocument);
        return RedirectToPage("/Index");
    }

    [HttpGet("Deliver/{orderId}/{customerId}")]
    public async Task<IActionResult> Deliver(string orderId, int customerId)
    {
        var versionedDocument = await _helper.RetrieveOrderAsync(orderId, customerId);
        _helper.DeliverOrder(versionedDocument);
        await _helper.SaveVersionedOrder(versionedDocument);
        return RedirectToPage("/Index");
    }

    [HttpGet("Fulfill/{orderId}/{customerId}")]
    public async Task<IActionResult> Fulfill(string orderId, int customerId)
    {
        var versionedDocument = await _helper.RetrieveOrderAsync(orderId, customerId);
        _helper.FulfillOrder(versionedDocument);
        await _helper.SaveVersionedOrder(versionedDocument);
        return RedirectToPage("/Index");
    }
}