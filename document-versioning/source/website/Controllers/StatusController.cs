using Microsoft.AspNetCore.Mvc;
using Versioning;

public class StatusController : Controller {

    private OrderHelper helper = new OrderHelper();

    [HttpGet("Cancel/{orderId}/{customerId}")]
    public async Task<IActionResult> Cancel(string orderId, int customerId){
        var versionedDocument = await helper.RetrieveOrderAsync(orderId, customerId);
        helper.CancelOrder(versionedDocument);
        await helper.SaveVersionedOrder(versionedDocument);
        return RedirectToPage("/Index");
    }

    [HttpGet("Deliver/{orderId}/{customerId}")]
    public async Task<IActionResult> Deliver(string orderId, int customerId)
    {
        var versionedDocument = await helper.RetrieveOrderAsync(orderId, customerId);
        helper.DeliverOrder(versionedDocument);
        await helper.SaveVersionedOrder(versionedDocument);
        return RedirectToPage("/Index");
    }

    [HttpGet("Fulfill/{orderId}/{customerId}")]
    public async Task<IActionResult> Fulfill(string orderId, int customerId)
    {
        var versionedDocument = await helper.RetrieveOrderAsync(orderId, customerId);
        helper.FulfillOrder(versionedDocument);
        await helper.SaveVersionedOrder(versionedDocument);
        return RedirectToPage("/Index");
    }
}