namespace HexagonalDdd.Order.Domain.Models
{
    /// <summary>
    /// Domain behavior added to the generated OrderBo partial class.
    /// </summary>
    public partial class OrderBo
    {
        public decimal Total => Quantity * UnitPrice * (1m - DiscountPercent / 100m);

        public void ApplyPriorityDiscount()
        {
            if (IsPriority && DiscountPercent < 10m)
            {
                DiscountPercent = 10m;
            }
        }
    }
}
