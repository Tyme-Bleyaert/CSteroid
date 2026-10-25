using System.Collections.Generic;
using HexagonalDdd.Customer.Domain.Models;

namespace HexagonalDdd.Customer.Application.Ports
{
    public interface ICustomerRepository
    {
        public void Save(CustomerBo customer);
        public CustomerBo? FindById(int id);
    }
}
