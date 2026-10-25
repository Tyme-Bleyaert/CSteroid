using System;
using CSteroid.Generated.Mappings;
using HexagonalDdd.Customer.Application;
using HexagonalDdd.Order.Application;
using HexagonalDdd.Ui.Infrastructure;
using HexagonalDdd.Ui.ViewModels;

// Composition root: wire the in-memory persistence adapters into the application services.
var customerService = new CustomerService(new InMemoryCustomerRepository());
var orderService = new OrderService(new InMemoryOrderRepository());

var failures = 0;
void Check(bool condition, string label)
{
    if (condition)
    {
        Console.WriteLine($"  OK   {label}");
    }
    else
    {
        failures++;
        Console.WriteLine($"  FAIL {label}");
    }
}

Console.WriteLine("== Customer flow (Entity -> Bo -> Dto -> ViewModel) ==");
var customerDto = customerService.RegisterCustomer("Ada Lovelace", "ada@example.com");
Check(customerDto.Id == 1, "customer id assigned by repository");
Check(customerDto.Name == "Ada Lovelace", "name survived entity/BO/DTO hops");

var customerVm = customerDto.MapToCustomerViewModel();
Check(customerVm.Email == "ada@example.com", "DTO mapped to view model");
Check(customerVm is CustomerViewModel, "view model is the generated Ui type");

Console.WriteLine("== Order flow with domain behavior ==");
var orderDto = orderService.PlaceOrder("Widget", 4, 25m, isPriority: true);
Check(orderDto.Id == 1, "order id assigned by repository");
Check(orderDto.DiscountPercent == 10m, "priority discount persisted (domain rule ran before save)");
Check(orderDto.Quantity * orderDto.UnitPrice * (1m - orderDto.DiscountPercent / 100m) == 90m, "total reflects discount across hops");

var orderVm = orderDto.MapToOrderViewModel();
Check(orderVm.IsPriority, "view model carries priority flag");
Check(orderVm.Product == "Widget", "product name reached the UI");

Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures;
