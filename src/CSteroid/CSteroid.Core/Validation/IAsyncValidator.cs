using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    public interface IAsyncValidator<T> : IValidator<T>
    {
        Task<ValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default);
    }
}
