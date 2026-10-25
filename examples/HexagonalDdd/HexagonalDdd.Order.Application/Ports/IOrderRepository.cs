using System.Collections.Generic;
using HexagonalDdd.Order.Domain.Models;

namespace HexagonalDdd.Order.Application.Ports
{
    /// <summary>
    /// Driven port: the persistence adapter implements this; the domain service only
    /// sees business objects.
    /// </summary>
    public interface IOrderRepository
    {
        public void Save(OrderBo order);
        public OrderBo? FindById(int id);
        public IReadOnlyList<OrderBo> FindAll();
    }
}
