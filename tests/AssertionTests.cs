namespace Microsoft.ContentAuthenticity.BindingTests;

public class AssertionTests
{
    [Fact]
    public void CreativeWorkAssertion_ShouldCreateWithCorrectLabel()
    {
        // Arrange
        var data = new CreativeWorkAssertionData("http://schema.org/", "CreativeWork");
        
        // Act
        var assertion = new CreativeWorkAssertion(data);
        
        // Assert
        Assert.Equal("stds.schema-org.CreativeWork", assertion.Label);
        Assert.Equal(data, assertion._Data);
        Assert.Equal(AssertionKind.Json, assertion.Kind);
    }

    [Fact]
    public void ActionAssertion_ShouldCreateWithCorrectLabel()
    {
        // Arrange
        var data = new ActionAssertionData();
        data.Actions.Add(new C2paAction("c2pa.edited"));
        
        // Act
        var assertion = new ActionAssertion(data);
        
        // Assert
        Assert.Equal("c2pa.action", assertion.Label);
        Assert.Equal(data, assertion._Data);
        Assert.Single(data.Actions);
    }

    [Fact]
    public void ThumbnailAssertion_ShouldCreateWithCorrectLabel()
    {
        // Arrange
        var data = new ThumbnailAssertionData("thumbnail.jpg", "thumb-123");
        
        // Act
        var assertion = new ThumbnailAssertion(data);
        
        // Assert
        Assert.Equal("c2pa.thumbnail", assertion.Label);
        Assert.Equal(data, assertion._Data);
    }

    [Fact]
    public void CustomAssertion_ShouldCreateWithCustomLabel()
    {
        // Arrange
        var label = "custom.assertion";
        var data = new { customField = "customValue" };
        
        // Act
        var assertion = new CustomAssertion(label, data);
        
        // Assert
        Assert.Equal(label, assertion.Label);
        Assert.Equal(data, assertion._Data);
    }

    [Fact]
    public void CreativeWorkAssertionData_WithAuthors_ShouldCreateCorrectly()
    {
        // Arrange
        var authors = new[] 
        { 
            new AuthorInfo("Person", "John Doe"),
            new AuthorInfo("Organization", "ACME Corp")
        };
        
        // Act
        var data = new CreativeWorkAssertionData("http://schema.org/", "CreativeWork", authors);
        
        // Assert
        Assert.Equal("http://schema.org/", data.Context);
        Assert.Equal("CreativeWork", data.Type);
        Assert.NotNull(data.Authors);
        Assert.Equal(2, data.Authors.Length);
        Assert.Equal("John Doe", data.Authors[0].Name);
        Assert.Equal("ACME Corp", data.Authors[1].Name);
    }

    [Fact]
    public void C2paAction_WithAllProperties_ShouldCreateCorrectly()
    {
        // Arrange
        var when = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var instanceId = Guid.NewGuid().ToString();
        
        // Act
        var action = new C2paAction(
            "c2pa.edited", 
            when, 
            "TestApp v1.0", 
            "color, brightness", 
            instanceId);
        
        // Assert
        Assert.Equal("c2pa.edited", action.Action);
        Assert.Equal(when, action.When);
        Assert.Equal("TestApp v1.0", action.SoftwareAgent);
        Assert.Equal("color, brightness", action.Changed);
        Assert.Equal(instanceId, action.InstanceID);
    }

    [Fact]
    public void Assertion_ToJson_ShouldSerializeCorrectly()
    {
        // Arrange
        var data = new CreativeWorkAssertionData("http://schema.org/", "CreativeWork");
        var assertion = new CreativeWorkAssertion(data);
        
        // Act
        var json = assertion.ToJson();
        
        // Assert
        Assert.NotNull(json);
        Assert.Contains("stds.schema-org.CreativeWork", json);
        Assert.Contains("http://schema.org/", json);
    }

    [Fact]
    public void Assertion_FromJson_ShouldDeserializeCorrectly()
    {
        // Arrange
        var original = new CreativeWorkAssertion(
            new CreativeWorkAssertionData("http://schema.org/", "CreativeWork"));
        var json = original.ToJson();
        
        // Act
        var deserialized = Assertion.FromJson(json);
        
        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Label, deserialized.Label);
    }
}