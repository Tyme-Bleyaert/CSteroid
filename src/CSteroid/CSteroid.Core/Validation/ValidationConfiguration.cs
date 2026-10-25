using System;

namespace CSteroid
{
    public sealed class ValidationConfiguration<T>
    {
        private readonly Action<Action<IRuleBuilder<T>>?> _setRules;
        private readonly Action<bool> _setReplace;

        public ValidationConfiguration(
            Action<Action<IRuleBuilder<T>>?> setRules,
            Action<bool> setReplace)
        {
            _setRules = setRules;
            _setReplace = setReplace;
        }

        public void AddRules(Action<IRuleBuilder<T>> configure)
            => _setRules(configure);

        public void ReplaceRules(Action<IRuleBuilder<T>> configure)
        {
            _setReplace(true);
            _setRules(configure);
        }
    }
}
