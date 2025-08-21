namespace Tag.DisplayRelay;

public sealed record RabbitOptions
{
    public string Uri { get; init; } = default!;
}
