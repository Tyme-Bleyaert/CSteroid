using System.Collections.Generic;

namespace CSteroid
{
    internal interface IRule<T>
    {
        void Evaluate(T model, List<ValidationFailure> failures);
    }
}
