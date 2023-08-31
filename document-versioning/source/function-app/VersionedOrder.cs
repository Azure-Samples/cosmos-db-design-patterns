namespace Versioning {
    public class VersionedOrder : Order {
        public int DocumentVersion { get; set; } = 1;

        public VersionedOrder(Order order){
            if (order != null)
            {
                this.OrderId = order.OrderId;
                this.OrderDate = order.OrderDate;
                this.CustomerId = order.CustomerId;
                this.Status = order.Status;
                this.OrderDetails = order.OrderDetails;
            }
        }
    }
}