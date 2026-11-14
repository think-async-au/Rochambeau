using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rochambeau.Api.Common.Http;

namespace Rochambeau.UnitTests.Common;

/// <summary>Framework-written error responses stay inside the contract (contracts/openapi.yaml · ProblemDetails).</summary>
public class ProblemDetailsDefaultsTests
{
    [Fact]
    public void A_body_the_framework_cannot_bind_is_a_400_with_common_invalid_request()
    {
        var http = new DefaultHttpContext();
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest };

        Customize(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });

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
