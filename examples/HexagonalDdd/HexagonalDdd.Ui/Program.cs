using System;
using CSteroid.Generated.Mappings;
using HexagonalDdd.Customer.Application;
using HexagonalDdd.Order.Application;
using HexagonalDdd.Ui.Infrastructure;
using HexagonalDdd.Ui.ViewModels;

// Composition root: wire the in-memory persistence adapters into the application services.
var customerService = new CustomerService(new InMemoryCustomerRepository());
var orderService = new OrderService(new InMemoryOrderRepository());

Console.WriteLine("== Customer flow (Entity -> Bo -> Dto -> ViewModel) ==");
var customerDto = customerService.RegisterCustomer("Ada Lovelace", "ada@example.com");
var customerVm = customerDto.MapToCustomerViewModel();
Console.WriteLine($"Customer {customerVm.Id}: {customerVm.Name} <{customerVm.Email}>");

Console.WriteLine("== Order flow with domain behavior ==");
var orderDto = orderService.PlaceOrder("Widget", 4, 25m, isPriority: true);
var orderVm = orderDto.MapToOrderViewModel();
Console.WriteLine(
    $"Order {orderVm.Id}: {orderVm.Quantity} x {orderVm.Product}, " +
    $"priority={orderVm.IsPriority}, discount={orderVm.DiscountPercent}%");
