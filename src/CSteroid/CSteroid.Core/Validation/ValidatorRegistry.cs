using System;
using System.Collections.Concurrent;

namespace CSteroid
{
    public static class ValidatorRegistry
    {
        private static readonly ConcurrentDictionary<Type, object> Validators = new ConcurrentDictionary<Type, object>();

        public static void Register<T>(IValidator<T> validator)
        {
            if (validator is null)
                throw new ArgumentNullException(nameof(validator));

            Validators[typeof(T)] = validator;
        }

        public static bool TryResolve<T>(out IValidator<T>? validator)
        {
            if (Validators.TryGetValue(typeof(T), out var existing))
            {
                validator = (IValidator<T>)existing;
                return true;
            }

            validator = null;
            return false;
        }
    }
}
