﻿using System;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;

namespace Nop.Plugin.Api.Factories
{
    public class OrderFactory : IFactory<Order>
    {
        public Order Initialize()
        {
            var order = new Order();

            order.CreatedOnUtc = DateTime.UtcNow;
            order.OrderGuid = Guid.NewGuid(); // majako changes: fix guid
            order.PaymentStatus = PaymentStatus.Pending;
            order.ShippingStatus = ShippingStatus.NotYetShipped;
            order.OrderStatus = OrderStatus.Pending;
            
            return order;
        }
    }
}