using Xunit;

public sealed class SlashCommandAttachmentResolverTests
{
    [Fact]
    public void FindFileValue_UsesTheOptionNameInsteadOfItsPosition()
    {
        var attachment = new object();
        var options = new (string Name, object? Value)[]
        {
            ("skip-prog-balancing", true),
            ("file", attachment)
        };

        Assert.Same(attachment, SlashCommandAttachmentResolver.FindFileValue(options));
    }
}
