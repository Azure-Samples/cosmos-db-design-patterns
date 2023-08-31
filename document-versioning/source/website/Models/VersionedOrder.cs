namespace Versioning {
    public class VersionedOrder : Order {
        public int DocumentVersion { get; set; } = 1;

        public VersionedOrder() {}
        public VersionedOrder(Order order){
            this.OrderId = order.OrderId;
            this.OrderDate = order.OrderDate;
            this.CustomerId = order.CustomerId;
            this.Status = order.Status;
            this.OrderDetails = order.OrderDetails;
        }
    }
}