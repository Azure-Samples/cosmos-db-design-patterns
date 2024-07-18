namespace Versioning.Models
{
    public class CartItemWithSpecialOrder : CartItem {
        public bool IsSpecialOrder { get; set; } = false;
        public string? SpecialOrderNotes {  get; set; }
    }
}