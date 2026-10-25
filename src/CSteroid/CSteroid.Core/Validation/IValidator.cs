namespace CSteroid
{
    public interface IValidator<T>
    {
        ValidationResult Validate(T instance);
    }
}
