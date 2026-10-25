using CSteroid;

namespace HexagonalDdd.Customer.Metadata
{
    /// <summary>
    /// Base contract shared by every Customer model.
    /// </summary>
    public interface ICustomerBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    /// <summary>
    /// All Customer model targets on one interface (see IOrderModelDefinition for why):
    ///   Domain      -> CustomerEntity + CustomerBo (explicit casts between them)
    ///   Application -> CustomerDto      (extension method CustomerBo -> CustomerDto)
    ///   Ui          -> CustomerViewModel (extension method CustomerDto -> CustomerViewModel)
    /// </summary>
    [CanonicalModelAttribute("CustomerEntity", CustomNamespace = "HexagonalDdd.Customer.Domain.Models", MappingTypes = MappingTypes.ExplicitCast)]
    [CanonicalModelAttribute("CustomerBo", CustomNamespace = "HexagonalDdd.Customer.Domain.Models", MappingTypes = MappingTypes.ExplicitCast | MappingTypes.ExtensionMethod)]
    [CanonicalModelAttribute("CustomerDto", CustomNamespace = "HexagonalDdd.Customer.Application.Dtos", MappingTypes = MappingTypes.ExtensionMethod)]
    [CanonicalModelAttribute("CustomerViewModel", CustomNamespace = "HexagonalDdd.Ui.ViewModels", MappingTypes = MappingTypes.ExtensionMethod)]
    public interface ICustomerModelDefinition : ICustomerBase
    {
    }
}
