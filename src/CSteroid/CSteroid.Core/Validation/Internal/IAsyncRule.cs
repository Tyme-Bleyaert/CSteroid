using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSteroid
{
    internal interface IAsyncRule<T>
    {
        Task EvaluateAsync(T model, List<ValidationFailure> failures, CancellationToken cancellationToken);
    }
}
