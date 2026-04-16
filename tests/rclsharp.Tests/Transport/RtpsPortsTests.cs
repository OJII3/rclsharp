using Rclsharp.Transport;

namespace Rclsharp.Tests.Transport;

public class RtpsPortsTests
{
    [Fact]
    public void Domain0_の_DiscoveryMulticast_は_7400()
    {
        RtpsPorts.DiscoveryMulticast(0).Should().Be(7400);
    }

    [Fact]
    public void Domain0_Participant0_の_DiscoveryUnicast_は_7410()
    {
        RtpsPorts.DiscoveryUnicast(0, 0).Should().Be(7410);
    }

    [Fact]
    public void Domain0_の_UserMulticast_は_7401()
    {
        RtpsPorts.UserMulticast(0).Should().Be(7401);
    }

    [Fact]
    public void Domain0_Participant0_の_UserUnicast_は_7411()
    {
        RtpsPorts.UserUnicast(0, 0).Should().Be(7411);
    }

    [Theory]
    [InlineData(0, 7400)]
    [InlineData(1, 7650)]   // 7400 + 250
    [InlineData(10, 9900)]  // 7400 + 250*10
    public void DiscoveryMulticast_は_PB_plus_DG_per_domain(int domain, int expectedPort)
    {
        RtpsPorts.DiscoveryMulticast(domain).Should().Be(expectedPort);
    }

    [Theory]
    [InlineData(0, 0, 7410)]
    [InlineData(0, 1, 7412)]   // +PG=2
    [InlineData(0, 5, 7420)]   // +PG*5=10
    [InlineData(1, 0, 7660)]   // +DG=250
    public void DiscoveryUnicast_は_PB_plus_DG_plus_d1_plus_PG(int domain, int participant, int expectedPort)
    {
        RtpsPorts.DiscoveryUnicast(domain, participant).Should().Be(expectedPort);
    }

    [Fact]
    public void Domain_範囲外は例外()
    {
        Action under = () => RtpsPorts.DiscoveryMulticast(-1);
        Action over = () => RtpsPorts.DiscoveryMulticast(233);
        under.Should().Throw<ArgumentOutOfRangeException>();
        over.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Participant_範囲外は例外()
    {
        Action under = () => RtpsPorts.DiscoveryUnicast(0, -1);
        Action over = () => RtpsPorts.DiscoveryUnicast(0, 120);
        under.Should().Throw<ArgumentOutOfRangeException>();
        over.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DefaultMulticastAddress_は_239_255_0_1()
    {
        RtpsConstants.DefaultMulticastAddress.ToString().Should().Be("239.255.0.1");
    }
}
