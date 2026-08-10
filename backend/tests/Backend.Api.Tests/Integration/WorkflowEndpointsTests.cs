using Backend.Api.Data;
using Backend.Api.Models.Dtos.Workflow;
using Backend.Api.Models.Dtos.WorkflowStep;
using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Backend.Api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
                CronExpression = "0 0 * * *",
                Timezone = "UTC"
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
                IsEnabled = false,
                TriggerType = "schedule",
                CronExpression = "0 0 * * *",
                Timezone = "UTC"
            };

            var createResponse = await client.PostAsJsonAsync("/workflow", createRequest);

            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var createdWorkflow = await createResponse.Content.ReadFromJsonAsync<WorkflowResponseDto>();

            createdWorkflow.Should().NotBeNull();
            createdWorkflow!.Id.Should().BeGreaterThan(0);
            createdWorkflow.Name.Should().Be("Nightly Workflow");

            var getAllResponse = await client.GetAsync("/workflow");

            getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var allWorkflows = await getAllResponse.Content.ReadFromJsonAsync<WorkflowListResponseDto>();

            allWorkflows.Should().NotBeNull();
            allWorkflows!.Items.Should().ContainSingle(workflow => workflow.Id == createdWorkflow.Id);
            allWorkflows.Page.Should().Be(1);
            allWorkflows.PageSize.Should().Be(WorkflowLimits.DefaultPageSize);
            allWorkflows.TotalCount.Should().Be(1);

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
                CronExpression = "*/5 * * * *",
                Timezone = "UTC"
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
                    IsEnabled = true,
                    Timezone = "UTC"
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
        public async Task WorkflowOwnership_ShouldUseIdentityUserId_NotProfileId()
        {
            await _factory.ResetDatabaseAsync();

            int firstProfileId;
            int secondIdentityUserId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var firstUser = new ApplicationUser
                {
                    Id = 100,
                    Email = "first@example.com",
                    UserName = "first@example.com",
                    NormalizedEmail = "FIRST@EXAMPLE.COM",
                    NormalizedUserName = "FIRST@EXAMPLE.COM"
                };

                var secondUser = new ApplicationUser
                {
                    Id = 200,
                    Email = "second@example.com",
                    UserName = "second@example.com",
                    NormalizedEmail = "SECOND@EXAMPLE.COM",
                    NormalizedUserName = "SECOND@EXAMPLE.COM"
                };

                db.Users.AddRange(firstUser, secondUser);
                await db.SaveChangesAsync();

                var firstProfile = new UserProfile
                {
                    IdentityUserId = firstUser.Id,
                    DisplayName = "First"
                };

                var secondProfile = new UserProfile
                {
                    IdentityUserId = secondUser.Id,
                    DisplayName = "Second"
                };

                db.UserProfile.AddRange(firstProfile, secondProfile);

                await db.SaveChangesAsync();

                firstProfileId = firstProfile.Id;
                secondIdentityUserId = secondUser.Id;

                firstProfileId.Should().NotBe(secondIdentityUserId);
            }

            var client = _factory.CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", WorkflowTestHelpers.CreateJwtToken(secondIdentityUserId));

            var response = await client.PostAsJsonAsync("/workflow", 
                new CreateWorkflowRequestDto
                {
                    Name = "Identity Owned Workflow",
                    IsEnabled = false,
                    TriggerType = "manual",
                    Timezone = "UTC"
                });

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            using var verificationScope = _factory.Services.CreateScope();

            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var storedWorkflow = await verificationDb.Workflow.SingleAsync();

            storedWorkflow.UserID.Should().Be(secondIdentityUserId);

            storedWorkflow.UserID.Should().NotBe(firstProfileId);
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
                    IsEnabled = true,
                    Timezone = "UTC"
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
                        StepKey = "http",
                        StepType = "http",
                        ConfigJson = "{\"url\":\"https://example.com/1\"}"
                    },
                    new WorkflowStepItemDto
                    {
                        StepKey = "email",
                        StepType = "email",
                        ConfigJson = "{\"to\":\"a@example.com\"}"
                    },
                    new WorkflowStepItemDto
                    {
                        StepKey = "delay",
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

        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        public async Task WorkflowStepEndpoints_ShouldReturnNotFound_ForOtherUsersWorkflow(string method)
        {
            await _factory.ResetDatabaseAsync();

            await SeedAuthenticatedUserAsync(userId: 1, email: "owner@example.com", displayName: "Owner");

            int workflowId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var workflow = new Workflow
                {
                    UserID = 1,
                    Name = "Private Workflow",
                    IsEnabled = true,
                    Timezone = "UTC"
                };

                db.Workflow.Add(workflow);
                await db.SaveChangesAsync();

                workflowId = workflow.ID;
            }

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId: 2, email: "attacker@example.com", displayName: "Other User");

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = [
                    new WorkflowStepItemDto
                    {
                        StepKey = "http",
                        StepType = "http",
                        ConfigJson = "{}"
                    }
                ]
            };

            HttpResponseMessage response = method switch
            {
                "GET" => await client.GetAsync($"/workflow/{workflowId}/step"),
                "POST" => await client.PostAsJsonAsync($"/workflow/{workflowId}/step", request),
                "PUT" => await client.PutAsJsonAsync($"/workflow/{workflowId}/step", request),
                "DELETE" => await client.DeleteAsync($"/workflow/{workflowId}/step"),
                _ => throw new InvalidOperationException()
            };

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task DeleteWorkflow_ShouldCascadeDeleteSteps()
        {
            await _factory.ResetDatabaseAsync();

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId: 1, email: "cascade@example.com", displayName: "Cascade User");

            int workflowId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var workflow = new Workflow
                {
                    UserID = 1,
                    Name = "Cascade Workflow",
                    IsEnabled = true,
                    Timezone = "UTC",
                    WorkflowSteps = [
                        new WorkflowStep
                        {
                            WorkflowID = 1,
                            StepKey = "first",
                            StepType = "first",
                            ConfigJson = "{}",
                            StepOrder = 1
                        },
                        new WorkflowStep
                        {
                            WorkflowID = 1,
                            StepKey = "second",
                            StepType = "second",
                            ConfigJson = "{}",
                            StepOrder = 2
                        }
                    ]
                };

                db.Workflow.Add(workflow);
                await db.SaveChangesAsync();

                workflowId = workflow.ID;
            }

            var response = await client.DeleteAsync($"/workflow/{workflowId}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using var verificationScope = _factory.Services.CreateScope();

            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var workflowExists = await verificationDb.Workflow.AnyAsync(workflow => workflow.ID == workflowId);

            var stepsExist = await verificationDb.WorkflowStep.AnyAsync(step => step.WorkflowID == workflowId);

            workflowExists.Should().BeFalse();
            stepsExist.Should().BeFalse();
        }

        [Fact]
        public async Task ReplaceWorkflowSteps_ShouldPersistContiguousOrder_InPostgreSql()
        {
            await _factory.ResetDatabaseAsync();

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId: 1, email: "replace@example.com", displayName: "Replace User");

            int workflowId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var workflow = new Workflow
                {
                    UserID = 1,
                    Name = "Replacement Workflow",
                    IsEnabled = true,
                    Timezone = "UTC",
                    WorkflowSteps = [
                        new WorkflowStep
                        {
                            WorkflowID = 1,
                            StepKey = "old_a",
                            StepType = "http",
                            ConfigJson = "{}",
                            StepOrder = 1
                        },
                        new WorkflowStep
                        {
                            WorkflowID = 1,
                            StepKey = "old_b",
                            StepType = "delay",
                            ConfigJson = "{}",
                            StepOrder = 2
                        }
                    ]
                };

                db.Workflow.Add(workflow);
                await db.SaveChangesAsync();

                workflowId = workflow.ID;
            }

            var response = await client.PutAsJsonAsync($"/workflow/{workflowId}/step",
                new SaveWorkflowStepsRequestDto
                {
                    Steps = [
                        new WorkflowStepItemDto
                        {
                            StepKey = "new_a",
                            StepType = "http",
                            ConfigJson = """{"value":1}"""
                        },
                        new WorkflowStepItemDto
                        {
                            StepKey = "new_b",
                            StepType = "delay",
                            ConfigJson = """{"value":2}"""
                        },
                        new WorkflowStepItemDto
                        {
                            StepKey = "new_c",
                            StepType = "email",
                            ConfigJson = """{"value":3}"""
                        }
                    ]
                });

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var verificationScope = _factory.Services.CreateScope();

            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var persistedSteps = await verificationDb.WorkflowStep
                    .Where(step => step.WorkflowID == workflowId)
                    .OrderBy(step => step.StepOrder)
                    .ToListAsync();

            persistedSteps.Select(step => step.StepOrder).Should().Equal(1, 2, 3);
            persistedSteps.Select(step => step.StepType).Should().Equal("http", "delay", "email");
            persistedSteps.Select(step => step.StepKey).Should().Equal("new_a", "new_b", "new_c");
        }

        [Fact]
        public async Task ReplaceWorkflowSteps_ShouldAllowEmptyList()
        {
            await _factory.ResetDatabaseAsync();

            const int userId = 7;

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId, "replace-empty@example.com", "Replace Empty User");

            var workflowId = await SeedWorkflowAsync(userId, "Replace Empty Workflow",
                (
                    StepKey: "existing",
                    StepType: "http",
                    ConfigJson: """{"a":1}""",
                    StepOrder: 1
                ));

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = []
            };

            var response = await client.PutAsJsonAsync($"/workflow/{workflowId}/step", request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<WorkflowStepListResponseDto>();

            payload.Should().NotBeNull();
            payload!.WorkflowId.Should().Be(workflowId);
            payload.Steps.Should().BeEmpty();

            using var verificationScope = _factory.Services.CreateScope();

            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var stepsExist = await verificationDb.WorkflowStep.AnyAsync(step => step.WorkflowID == workflowId);

            stepsExist.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteWorkflowSteps_ShouldSucceed_WhenNoStepsExist()
        {
            await _factory.ResetDatabaseAsync();

            const int userId = 7;

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId, "delete-empty@example.com", "Delete Empty User");

            var workflowId = await SeedWorkflowAsync(userId, "No Steps Workflow");

            var response = await client.DeleteAsync($"/workflow/{workflowId}/step");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using var verificationScope = _factory.Services.CreateScope();

            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var workflowExists = await verificationDb.Workflow.AnyAsync(workflow => workflow.ID == workflowId);

            var stepsExist = await verificationDb.WorkflowStep.AnyAsync(step => step.WorkflowID == workflowId);

            workflowExists.Should().BeTrue();
            stepsExist.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteWorkflowSteps_ShouldDeleteAllSteps_WhenTheyExist()
        {
            await _factory.ResetDatabaseAsync();

            const int userId = 7;

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId, "delete-steps@example.com", "Delete Steps User");

            var workflowId = await SeedWorkflowAsync(userId, "Delete Steps Workflow",
                (
                    StepKey: "a",
                    StepType: "http",
                    ConfigJson: """{"a":1}""",
                    StepOrder: 1
                ),
                (
                    StepKey: "b",
                    StepType: "delay",
                    ConfigJson: """{"b":2}""",
                    StepOrder: 2
                ));

            var response = await client.DeleteAsync($"/workflow/{workflowId}/step");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            using var verificationScope = _factory.Services.CreateScope();

            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var workflowExists = await verificationDb.Workflow.AnyAsync(workflow => workflow.ID == workflowId);

            var remainingSteps = await verificationDb.WorkflowStep
                .Where(step => step.WorkflowID == workflowId)
                .ToListAsync();

            workflowExists.Should().BeTrue();
            remainingSteps.Should().BeEmpty();
        }

        [Fact]
        public async Task ReplaceWorkflowSteps_ShouldReplaceAllExistingSteps()
        {
            await _factory.ResetDatabaseAsync();

            const int userId = 7;

            var client = await CreateAuthenticatedClientWithSeededUserAsync(userId, "replace-all@example.com", "Replace All User");

            var workflowId = await SeedWorkflowAsync(userId, "Replace Workflow",
                (
                    StepKey: "old_1", 
                    StepType: "http",
                    ConfigJson: """{"a":1}""",
                    StepOrder: 1
                ),
                (
                    StepKey: "old_2",
                    StepType: "delay",
                    ConfigJson: """{"b":2}""",
                    StepOrder: 2
                ));

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = [
                    new WorkflowStepItemDto
                    {
                        StepKey = "new_1",
                        StepType = "http",
                        ConfigJson = """{"x":1}"""
                    },
                    new WorkflowStepItemDto
                    {
                        StepKey = "new_2",
                        StepType = "delay",
                        ConfigJson = """{"y":2}"""
                    },
                    new WorkflowStepItemDto
                    {
                        StepKey = "new_3",
                        StepType = "email",
                        ConfigJson = """{"z":3}"""
                    }
                ]
            };

            var response = await client.PutAsJsonAsync($"/workflow/{workflowId}/step", request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var payload = await response.Content.ReadFromJsonAsync<WorkflowStepListResponseDto>();

            payload.Should().NotBeNull();
            payload!.WorkflowId.Should().Be(workflowId);
            payload.Steps.Select(step => step.StepType).Should().Equal("http", "delay", "email");
            payload.Steps.Select(step => step.StepKey).Should().Equal("new_1", "new_2", "new_3");
            payload.Steps.Select(step => step.StepOrder).Should().Equal(1, 2, 3);

            using var verificationScope = _factory.Services.CreateScope();

            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var savedSteps = await verificationDb.WorkflowStep
                .Where(step => step.WorkflowID == workflowId)
                .OrderBy(step => step.StepOrder)
                .ToListAsync();

            savedSteps.Should().HaveCount(3);
            savedSteps.Select(step => step.StepType).Should().Equal("http", "delay", "email");
            savedSteps.Select(step => step.StepKey).Should().Equal("new_1", "new_2", "new_3");
            savedSteps.Select(step => step.StepOrder).Should().Equal(1, 2, 3);
            savedSteps.Should().NotContain(step => step.StepKey == "old_1" || step.StepKey == "old_2");
        }

        private async Task<int> SeedWorkflowAsync(int userId, string name, params (string StepKey, string StepType, string ConfigJson, int StepOrder)[] steps)
        {
            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var workflow = new Workflow
            {
                UserID = userId,
                Name = name,
                IsEnabled = true,
                Timezone = "UTC"
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            if (steps.Length > 0)
            {
                var workflowSteps = steps.Select(step => new WorkflowStep
                {
                    WorkflowID = workflow.ID,
                    StepKey = step.StepKey,
                    StepType = step.StepType,
                    ConfigJson = step.ConfigJson,
                    StepOrder = step.StepOrder
                });

                db.WorkflowStep.AddRange(workflowSteps);
                await db.SaveChangesAsync();
            }

            return workflow.ID;
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

        private async Task<HttpClient> CreateAuthenticatedClientWithSeededUserAsync(int userId, string email = "user@example.com", string displayName = "Test User")
        {
            await SeedAuthenticatedUserAsync(userId, email, displayName);

            var client = _factory.CreateClient();
            var token = WorkflowTestHelpers.CreateJwtToken(userId);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }
    }
}
