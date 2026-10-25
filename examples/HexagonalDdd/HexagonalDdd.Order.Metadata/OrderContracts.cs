using System;
using CSteroid;

namespace HexagonalDdd.Order.Metadata
{
    /// <summary>
    /// Base contract shared by every Order model: persistence entity, business object,
    /// DTO and view model.
    /// </summary>
    public interface IOrderBase
    {
        public int Id { get; set; }
        public string Product { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>
    /// All Order model targets on one interface: mapping pairs form only between targets
    /// declared on the same interface. Each layer project claims its own namespace via
    /// [assembly: GeneratorConfig(GenerateOnlyNamespaces = ...)] and generates just its
    /// slice:
    ///   Domain      -> OrderEntity + OrderBo (explicit casts between them)
    ///   Application -> OrderDto      (extension method OrderBo -> OrderDto)
    ///   Ui          -> OrderViewModel (extension method OrderDto -> OrderViewModel)
    /// </summary>
    [CanonicalModelAttribute("OrderEntity", CustomNamespace = "HexagonalDdd.Order.Domain.Models", MappingTypes = MappingTypes.ExplicitCast)]
    [CanonicalModelAttribute("OrderBo", CustomNamespace = "HexagonalDdd.Order.Domain.Models", MappingTypes = MappingTypes.ExplicitCast | MappingTypes.ExtensionMethod)]
    [CanonicalModelAttribute("OrderDto", CustomNamespace = "HexagonalDdd.Order.Application.Dtos", MappingTypes = MappingTypes.ExtensionMethod)]
    [CanonicalModelAttribute("OrderViewModel", CustomNamespace = "HexagonalDdd.Ui.ViewModels", MappingTypes = MappingTypes.ExtensionMethod)]
    public interface IOrderModelDefinition : IOrderBase
    {
        public bool IsPriority { get; set; }
    }
}
