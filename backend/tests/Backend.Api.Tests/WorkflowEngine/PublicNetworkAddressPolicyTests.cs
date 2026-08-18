using Backend.Api.WorkflowEngine.Http;
using FluentAssertions;
using System.Net;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class PublicNetworkAddressPolicyTests
    {
        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("10.0.0.1")]
        [InlineData("172.16.0.1")]
        [InlineData("192.168.1.1")]
        [InlineData("169.254.169.254")]
        [InlineData("100.64.0.1")]
        [InlineData("192.0.2.1")]
        [InlineData("198.51.100.1")]
        [InlineData("203.0.113.1")]
        [InlineData("::1")]
        [InlineData("fc00::1")]
        [InlineData("fe80::1")]
        [InlineData("2001:db8::1")]
        [InlineData("::ffff:127.0.0.1")]
        [InlineData("64:ff9b::a00:1")]
        [InlineData("2002:0a00:0001::1")]
        public void IsAllowed_ShouldRejectNonPublicAddresses(string value)
        {
            PublicNetworkAddressPolicy.IsAllowed(IPAddress.Parse(value)).Should().BeFalse();
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("1.1.1.1")]
        [InlineData("2606:4700:4700::1111")]
        public void IsAllowed_ShouldAcceptPublicAddresses(string value)
        {
            PublicNetworkAddressPolicy.IsAllowed(IPAddress.Parse(value)).Should().BeTrue();
        }
    }
}