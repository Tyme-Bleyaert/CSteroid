namespace CSteroid
{
    public interface IMapper<TSource, TDest>
    {
        TDest Map(TSource source);
    }
}
