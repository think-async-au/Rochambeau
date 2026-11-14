using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rochambeau.Api.Common.Http;

namespace Rochambeau.UnitTests.Common;

/// <summary>Framework-written error responses stay inside the contract (contracts/openapi.yaml · ProblemDetails).</summary>
public class ProblemDetailsDefaultsTests
{
    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType)]
    public void A_body_the_framework_cannot_read_is_a_400_with_common_invalid_request(int frameworkStatus)
    {
        var http = new DefaultHttpContext();
        http.Response.StatusCode = frameworkStatus;
        var problem = new ProblemDetails { Status = frameworkStatus };

        Customize(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });

        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("common.invalid_request", problem.Extensions["errorCode"]);
        Assert.Equal("The request could not be read.", problem.Title);
    }

    [Fact]
    public void A_problem_that_already_has_an_error_code_is_left_alone()
    {
        var http = new DefaultHttpContext();
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problem = new ProblemDetails { Status = 400, Title = "Some field is wrong." };
        problem.Extensions["errorCode"] = "identity.validation_failed";

        Customize(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });

        Assert.Equal("identity.validation_failed", problem.Extensions["errorCode"]);
        Assert.Equal("Some field is wrong.", problem.Title);
    }

    private static void Customize(ProblemDetailsContext context)
    {
        using var services = new ServiceCollection().AddProblemDetailsWithErrorCodes().BuildServiceProvider();
        var customize = services.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails;
        Assert.NotNull(customize);
        customize(context);
    }
}
