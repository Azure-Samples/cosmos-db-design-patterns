using System;
using System.Collections.Generic;

namespace Versioning
{
    public class OrderHelper
    {
        public static Order GenerateOrder() {
            var order = new Order();
            Random rng = new Random();
            order.OrderId = Guid.NewGuid().ToString();            
            order.CustomerId = rng.Next(1,999);
            order.Status = "Submitted";
            order.OrderDetails = new List<OrderItem>();
            int orderItemCount = rng.Next(1, 5);
            for (int i=0; i < orderItemCount; i++)
            {
                order.OrderDetails.Add(GenerateOrderItem());
            }
            return order;
        }

        private static VersionedOrder HandleVersioning(Order order){
            VersionedOrder versionedOrder;
            if (order is not VersionedOrder){
                versionedOrder = new VersionedOrder(order);
            } else {
                versionedOrder = (VersionedOrder)order;
                versionedOrder.DocumentVersion++;
            }
            return versionedOrder;
        }

        public static VersionedOrder CancelOrder(Order order) {
            order.id = Guid.NewGuid().ToString();
            order.Status = "Cancelled";
            return HandleVersioning(order);
        }

        public static VersionedOrder FulfillOrder(Order order) {
            order.id = Guid.NewGuid().ToString();
            order.Status = "Fulfilled";
            return HandleVersioning(order);
        }

        public static VersionedOrder DeliverOrder(Order order) {
            order.id = Guid.NewGuid().ToString();
            order.Status = "Delivered";
            return HandleVersioning(order);
        }

        public static OrderItem GenerateOrderItem()
        {
            var OrderItem = new OrderItem();
            Random rng = new Random();
            var productId = rng.Next(1, 25);
            OrderItem.ProductName = $"Product {productId}";
            OrderItem.Quantity = rng.Next(1, 5);
            return OrderItem;
        }
    }
}