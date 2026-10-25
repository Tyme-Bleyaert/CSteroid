using Riok.Mapperly.Abstractions;

namespace CSteroid.Benchmarks;

[Mapper]
public partial class BenchMapper
{
    public partial PlainUserDto Map(PlainUser source);

    public partial PlainFlatDto MapFlat(PlainFlat source);
}
