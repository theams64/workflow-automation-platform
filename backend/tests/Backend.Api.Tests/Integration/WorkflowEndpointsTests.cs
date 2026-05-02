using Backend.Api.Data;
using Backend.Api.Models.Dtos.Workflow;
using Backend.Api.Models.Dtos.WorkflowStep;
using Backend.Api.Models.Entities;
using Backend.Api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Backend.Api.Tests.Integration
{
    public sealed class WorkflowEndpointsTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public WorkflowEndpointsTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CreateWorkflow_ShouldReturnUnauthorized_WhenNoTokenIsProvided()
        {
            await _factory.ResetDatabaseAsync();
            var client = _factory.CreateClient();

            var request = new CreateWorkflowRequestDto
            {
                Name = "No Auth Workflow",
                IsEnabled = true,
                TriggerType = "schedule",
                CronExpression = "0 0 * * *"
            };

            var response = await client.PostAsJsonAsync("/workflow", request);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task WorkflowLifecycle_ShouldSucceed_ForAuthenticatedUser()
        {
            await _factory.ResetDatabaseAsync();
            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId: 1, email: "user1@example.com", displayName: "User One");

            var createRequest = new CreateWorkflowRequestDto
            {
                Name = "Nightly Workflow",
                IsEnabled = true,
                TriggerType = "schedule",
                CronExpression = "0 0 * * *"
            };

            var createResponse = await client.PostAsJsonAsync("/workflow", createRequest);

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var createdWorkflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponseDto>();

            createdWorkflow.Should().NotBeNull();
            createdWorkflow!.Id.Should().BeGreaterThan(0);
            createdWorkflow.Name.Should().Be("Nightly Workflow");

            var getAllResponse = await client.GetAsync("/workflow");

            getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var allWorkflows = await getAllResponse.Content.ReadFromJsonAsync<List<WorkflowResponseDto>>();

            allWorkflows.Should().NotBeNull();
            allWorkflows!.Should().ContainSingle(x => x.Id == createdWorkflow.Id);

            var getByIdResponse = await client.GetAsync($"/workflow/{createdWorkflow.Id}");

            getByIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var workflowDetail = await getByIdResponse.Content.ReadFromJsonAsync<WorkflowDetailResponseDto>();

            workflowDetail.Should().NotBeNull();
            workflowDetail!.Id.Should().Be(createdWorkflow.Id);
            workflowDetail.Name.Should().Be("Nightly Workflow");

            var updateRequest = new UpdateWorkflowRequestDto
            {
                Name = "Updated Nightly Workflow",
                IsEnabled = false,
                TriggerType = "schedule",
                CronExpression = "*/5 * * * *"
            };

            var updateResponse = await client.PutAsJsonAsync($"/workflow/{createdWorkflow.Id}", updateRequest);

            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var updatedWorkflow = await updateResponse.Content.ReadFromJsonAsync<WorkflowResponseDto>();

            updatedWorkflow.Should().NotBeNull();
            updatedWorkflow!.Name.Should().Be("Updated Nightly Workflow");
            updatedWorkflow.IsEnabled.Should().BeFalse();
            updatedWorkflow.CronExpression.Should().Be("*/5 * * * *");

            var deleteResponse = await client.DeleteAsync($"/workflow/{createdWorkflow.Id}");

            deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var getAfterDeleteResponse = await client.GetAsync($"/workflow/{createdWorkflow.Id}");

            getAfterDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetWorkflowById_ShouldReturnNotFound_ForDifferentUser()
        {
            await _factory.ResetDatabaseAsync();

            int workflowId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await SeedAuthenticatedUserAsync(userId: 1, email: "user1@example.com", displayName: "User One");

                var workflow = new Workflow
                {
                    UserID = 1,
                    Name = "Other User Workflow",
                    IsEnabled = true
                };

                db.Workflow.Add(workflow);
                await db.SaveChangesAsync();
                workflowId = workflow.ID;
            }

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId: 2, email: "user2@example.com", displayName: "User Two");

            var response = await client.GetAsync($"/workflow/{workflowId}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateWorkflowSteps_ThenGetWorkflowSteps_ShouldReturnOrderedSteps()
        {
            await _factory.ResetDatabaseAsync();

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId: 1, email: "user1@example.com", displayName: "User One");

            int workflowId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var workflow = new Workflow
                {
                    UserID = 1,
                    Name = "Workflow With Steps",
                    IsEnabled = true
                };

                db.Workflow.Add(workflow);
                await db.SaveChangesAsync();

                workflowId = workflow.ID;
            }

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps =
                [
                    new WorkflowStepItemDto
                    {
                        StepType = "http",
                        ConfigJson = "{\"url\":\"https://example.com/1\"}"
                    },
                    new WorkflowStepItemDto
                    {
                        StepType = "email",
                        ConfigJson = "{\"to\":\"a@example.com\"}"
                    },
                    new WorkflowStepItemDto
                    {
                        StepType = "delay",
                        ConfigJson = "{\"seconds\":30}"
                    }
                ]
            };

            var createResponse = await client.PostAsJsonAsync($"/workflow/{workflowId}/step", request);

            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var createPayload = await createResponse.Content.ReadFromJsonAsync<WorkflowStepListResponseDto>();

            createPayload.Should().NotBeNull();
            createPayload!.WorkflowId.Should().Be(workflowId);
            createPayload.Steps.Select(x => x.StepOrder).Should().Equal(1, 2, 3);

            var getResponse = await client.GetAsync($"/workflow/{workflowId}/step");

            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var getPayload = await getResponse.Content.ReadFromJsonAsync<WorkflowStepListResponseDto>();

            getPayload.Should().NotBeNull();
            getPayload!.Steps.Should().HaveCount(3);
            getPayload.Steps.Select(x => x.StepType).Should().Equal("http", "email", "delay");
            getPayload.Steps.Select(x => x.StepOrder).Should().Equal(1, 2, 3);
        }

        private async Task SeedAuthenticatedUserAsync(int userId, string email = "user@example.com:", string displayName = "Test User")
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existingIdentityUser = await db.Users.FindAsync(userId);
            if (existingIdentityUser == null)
            {
                db.Users.Add(new ApplicationUser
                {
                    Id = userId,
                    Email = email,
                    UserName = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    NormalizedUserName = email.ToUpperInvariant()
                });
            }

            var existingProfile = await db.UserProfile.FirstOrDefaultAsync(x => x.IdentityUserId == userId);
            if (existingProfile == null)
            {
                db.UserProfile.Add(new UserProfile
                {
                    IdentityUserId = userId,
                    DisplayName = displayName
                });
            }

            await db.SaveChangesAsync();
        }

        private async Task<HttpClient> CreateAuthenticatedClientWithSeededUserAsync(int userId, string email = "user@example.com:", string displayName = "Test User")
        {
            await SeedAuthenticatedUserAsync(userId, email, displayName);
            
            var client = _factory.CreateClient();
            var token = WorkflowTestHelpers.CreateJwtToken(userId);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }
    }
}
