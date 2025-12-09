using FluentAssertions;
using App.Domain.Entities;
using App.Domain.Exceptions;

namespace App.Domain.UnitTests.ValueObjects;

public class BuiltInEmailTemplateTests
{
    [Test]
    [TestCase("email_admin_welcome")]
    [TestCase("email_admin_passwordchanged")]
    [TestCase("email_admin_passwordreset")]
    [TestCase("email_login_beginloginwithmagiclink")]
    [TestCase("email_login_beginforgotpassword")]
    [TestCase("email_login_completedforgotpassword")]
    [TestCase("email_user_welcome")]
    [TestCase("email_user_passwordchanged")]
    [TestCase("email_user_passwordreset")]
    [Parallelizable(ParallelScope.All)]
    public void ShouldReturnCorrectDeveloperName(string developerName)
    {
        var type = BuiltInEmailTemplate.From(developerName);
        type.DeveloperName.Should().Be(developerName);
    }

    [Test]
    public void ShouldPerformImplicitConversionToString()
    {
        string type = BuiltInEmailTemplate.AdminWelcomeEmail;

        type.Should().Be("email_admin_welcome");
    }

    [Test]
    public void ShouldPerformExplicitConversionGivenSupportedType()
    {
        var type = (BuiltInEmailTemplate)"email_admin_welcome";

        type.Should().Be(BuiltInEmailTemplate.AdminWelcomeEmail);
    }

    [Test]
    public void ShouldThrowUnsupportedColourExceptionGivenNotSupportedColourCode()
    {
        FluentActions
            .Invoking(() => BuiltInEmailTemplate.From("BadValue"))
            .Should()
            .Throw<UnsupportedTemplateTypeException>();
    }

    [Test]
    public void ShouldMatchNumberOfSupportedTypes()
    {
        BuiltInEmailTemplate.Templates.Count().Should().Be(9);
    }
}
