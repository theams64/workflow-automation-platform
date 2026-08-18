using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Http;
using FluentAssertions;
using Moq;
using System.Net;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class SafeHttpMessageHandlerFactoryTests
    {
        [Fact]
        public void Create_ShouldDisableAmbientOutboundFeatures()
        {
            var options = new SafeHttpOptions();
            var connector = new Mock<IPublicNetworkConnector>();

            using var handler = SafeHttpMessageHandlerFactory.Create(options, connector.Object);

            handler.AllowAutoRedirect.Should().BeFalse();
            handler.UseCookies.Should().BeFalse();
            handler.UseProxy.Should().BeFalse();
            handler.Proxy.Should().BeNull();
            handler.Credentials.Should().BeNull();
            handler.PreAuthenticate.Should().BeFalse();
            handler.AutomaticDecompression.Should().Be(DecompressionMethods.None);
            handler.ActivityHeadersPropagator.Should().BeNull();
            handler.ConnectCallback.Should().NotBeNull();
        }
    }
}