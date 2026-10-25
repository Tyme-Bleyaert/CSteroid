using CSteroid.Generated.Mappings;
using HexagonalDdd.Order.Application.Dtos;
using HexagonalDdd.Order.Application.Ports;
using HexagonalDdd.Order.Domain.Models;

namespace HexagonalDdd.Order.Application
{
    /// <summary>
    /// Application service: drives the domain, returns DTOs across the boundary.
    /// BO→DTO extension mapping is generated in this assembly.
    /// </summary>
    public class OrderService
    {
        private readonly IOrderRepository _orders;

        public OrderService(IOrderRepository orders)
        {
            _orders = orders;
        }

        public OrderDto PlaceOrder(string product, int quantity, decimal unitPrice, bool isPriority)
        {
            var bo = new OrderBo
            {
                Id = 0,
                Product = product,
                Quantity = quantity,
                UnitPrice = unitPrice,
                DiscountPercent = 0m,
                CreatedUtc = System.DateTime.UtcNow,
                IsPriority = isPriority,
            };

            bo.ApplyPriorityDiscount();
            _orders.Save(bo);

            return bo.MapToOrderDto();
        }

        public OrderDto? GetOrder(int id)
        {
            return _orders.FindById(id)?.MapToOrderDto();
        }
    }
}
