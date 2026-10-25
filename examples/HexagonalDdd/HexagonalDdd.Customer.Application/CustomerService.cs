using CSteroid.Generated.Mappings;
using HexagonalDdd.Customer.Application.Dtos;
using HexagonalDdd.Customer.Application.Ports;
using HexagonalDdd.Customer.Domain.Models;

namespace HexagonalDdd.Customer.Application
{
    public class CustomerService
    {
        private readonly ICustomerRepository _customers;

        public CustomerService(ICustomerRepository customers)
        {
            _customers = customers;
        }

        public CustomerDto RegisterCustomer(string name, string email)
        {
            var bo = new CustomerBo
            {
                Id = 0,
                Name = name,
                Email = email,
            };

            _customers.Save(bo);

            return bo.MapToCustomerDto();
        }

        public CustomerDto? GetCustomer(int id)
        {
            return _customers.FindById(id)?.MapToCustomerDto();
        }
    }
}
