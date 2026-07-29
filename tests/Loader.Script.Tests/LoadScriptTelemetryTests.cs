using System.Diagnostics;

namespace Loader.Script.Tests;

public sealed class LoadScriptTelemetryTests
{
    [Test]
    [DisplayName("Telemetry tag sanitizing скрывает password и pwd во всех string tag values")]
    public async Task Set_sanitized_tag_redacts_password_like_parts()
    {
        // Arrange
        using var activity = new Activity("test").Start();

        // Act
        activity.SetSanitizedTag("source", "Host=localhost;Password=secret;Pwd=short;User=loader");

        // Assert
        var tag = activity.Tags.Single(static tag => tag.Key == "source");
        await Assert.That(tag.Value).IsEqualTo("Host=localhost;Password=***;Pwd=***;User=loader");
    }

    [Test]
    [DisplayName("Telemetry tag sanitizing поддерживает chaining после обычного SetTag")]
    public async Task Set_sanitized_tag_supports_chaining_after_set_tag()
    {
        // Arrange
        using var activity = new Activity("test").Start();

        // Act
        activity
            .SetTag("index", 42)
            .SetSanitizedTag("source", "Password=secret");

        // Assert
        await Assert.That(activity.TagObjects.Single(static tag => tag.Key == "index").Value).IsEqualTo(42);
        await Assert.That(activity.Tags.Single(static tag => tag.Key == "source").Value).IsEqualTo("Password=***");
    }
}
