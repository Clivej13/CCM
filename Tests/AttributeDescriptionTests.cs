using CCM.Application;
using CCM.Game;
using Xunit;

namespace CCM.Tests;

public sealed class AttributeDescriptionTests
{
    [Fact]
    public void BundledDescriptionsCoverEveryAttribute()
    {
        var catalogue=Settings.Required<AttributeDescriptionCatalogue>(
            Settings.ConfigPath("attributes.json"));
        catalogue.Validate();
        foreach (var kind in Enum.GetValues<AttributeKind>())
        {
            var entry=catalogue.Get(kind);
            Assert.Equal(kind,entry.Kind);
            Assert.False(string.IsNullOrWhiteSpace(entry.Summary));
            Assert.False(string.IsNullOrWhiteSpace(entry.Details));
        }
    }

    [Fact]
    public void MissingDuplicateAndUnknownDescriptionsAreRejected()
    {
        var catalogue=Settings.Required<AttributeDescriptionCatalogue>(
            Settings.ConfigPath("attributes.json"));
        var original=catalogue.Attributes[0];
        catalogue.Attributes[0]=catalogue.Attributes[1];
        Assert.Throws<InvalidDataException>(catalogue.Validate);
        catalogue.Attributes[0]=original;
        catalogue.Attributes.RemoveAt(0);
        Assert.Throws<InvalidDataException>(catalogue.Validate);
        catalogue.Attributes.Insert(0,new AttributeDescription
        {
            Attribute="Unknown",Summary="Unknown",Details="Unknown"
        });
        Assert.Throws<InvalidDataException>(catalogue.Validate);
    }
}
