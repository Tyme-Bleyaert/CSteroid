using System.Collections.Generic;
using HexagonalDdd.Customer.Application.Ports;
using HexagonalDdd.Customer.Domain.Models;

namespace HexagonalDdd.Ui.Infrastructure
{
    public class InMemoryCustomerRepository : ICustomerRepository
    {
        private readonly Dictionary<int, CustomerEntity> _rows = new();
        private int _nextId = 1;

        public void Save(CustomerBo customer)
        {
            if (customer.Id == 0)
            {
                customer.Id = _nextId++;
            }

            _rows[customer.Id] = (CustomerEntity)customer;
        }

        public CustomerBo? FindById(int id)
        {
            return _rows.TryGetValue(id, out var entity) ? (CustomerBo)entity : null;
        }
    }
}
