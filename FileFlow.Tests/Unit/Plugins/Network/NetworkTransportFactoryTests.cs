using FileFlow.Plugin.Network.Transports;
using FluentAssertions;
using Xunit;

namespace FileFlow.Tests.Unit.Plugins.Network;

public class NetworkTransportFactoryTests
{
    [Theory]
    [InlineData("HTTP", typeof(HttpTransportStrategy))]
    [InlineData("https", typeof(HttpTransportStrategy))]
    [InlineData(" FTP ", typeof(FtpTransportStrategy))]
    [InlineData("ftps", typeof(FtpTransportStrategy))]
    [InlineData("sftp", typeof(SftpTransportStrategy))]
    [InlineData("ssh", typeof(SftpTransportStrategy))]
    [InlineData("webdav", typeof(WebDavTransportStrategy))]
    [InlineData("SMB", typeof(SmbTransportStrategy))]
    [InlineData("unc", typeof(SmbTransportStrategy))]
    public void GetTransport_ShouldResolveExpectedStrategy(string protocol, Type expectedType)
    {
        var transport = NetworkTransportFactory.GetTransport(protocol);

        transport.Should().BeOfType(expectedType);
    }

    [Fact]
    public void GetTransport_WithUnsupportedProtocol_ShouldThrowNotSupportedException()
    {
        var action = () => NetworkTransportFactory.GetTransport("gopher");

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*gopher*");
    }
}
