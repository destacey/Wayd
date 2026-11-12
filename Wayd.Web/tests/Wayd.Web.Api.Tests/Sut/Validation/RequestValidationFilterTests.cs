using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Web.Api.Validation;

namespace Wayd.Web.Api.Tests.Sut.Validation;

/// <summary>
/// The filter that runs a request's FluentValidation validator before the action: failures land in
/// <c>ModelState</c> under their property paths, the runtime type picks the validator, and the filter never
/// answers on its own.
/// </summary>
public sealed class RequestValidationFilterTests
{
    private readonly RequestValidationFilter _sut = new();

    [Fact]
    public async Task OnActionExecutionAsync_RecordsEachFailureUnderItsPropertyPath()
    {
        // Arrange
        var context = ContextFor(new Order { Name = "", Lines = [new Line { Label = "Kept" }, new Line { Label = "" }] });

        // Act
        var nextCalled = await Execute(context);

        // Assert
        context.ModelState.IsValid.Should().BeFalse();
        context.ModelState.Keys.Should().BeEquivalentTo("Name", "Lines[1].Label");
        context.ModelState["Name"]!.Errors.Select(e => e.ErrorMessage).Should().Equal("An order needs a name.");
        nextCalled.Should().BeTrue("the model-state filter answers, not this one");
    }

    [Fact]
    public async Task OnActionExecutionAsync_LeavesModelStateValid_WhenTheArgumentPasses()
    {
        // Arrange
        var context = ContextFor(new Order { Name = "Spring", Lines = [new Line { Label = "Kept" }] });

        // Act
        var nextCalled = await Execute(context);

        // Assert
        context.ModelState.IsValid.Should().BeTrue();
        context.ModelState.Should().BeEmpty();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_PicksTheValidatorByTheArgumentsRuntimeType()
    {
        // Arrange — declared as the abstract base, as a polymorphic body is; only the concrete type has a validator
        Shape argument = new Circle { Radius = 0 };
        var context = ContextFor(argument);

        // Act
        await Execute(context);

        // Assert
        context.ModelState.Keys.Should().Equal("Radius");
    }

    [Fact]
    public async Task OnActionExecutionAsync_SkipsArgumentsThatAreNullOrHaveNoValidator()
    {
        // Arrange
        var context = ContextFor(null, "a route value", Guid.NewGuid(), new Unvalidated());

        // Act
        var nextCalled = await Execute(context);

        // Assert
        context.ModelState.Should().BeEmpty();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_RunsAsyncRulesWithTheRequestsCancellationToken()
    {
        // Arrange
        using var aborted = new CancellationTokenSource();
        var context = ContextFor(new Lookup { Key = "taken" });
        context.HttpContext.RequestAborted = aborted.Token;

        // Act
        await Execute(context);

        // Assert
        context.ModelState.Keys.Should().Equal("Key");
        LookupValidator.LastToken.Should().Be(aborted.Token);
    }

    [Fact]
    public void Order_RunsAheadOfTheModelStateInvalidFilter()
    {
        // Arrange
        var modelStateFilter = new ModelStateInvalidFilter(
            new ApiBehaviorOptions { InvalidModelStateResponseFactory = _ => new EmptyResult() },
            NullLogger.Instance);

        // Act / Assert
        RequestValidationFilter.Order.Should().BeLessThan(modelStateFilter.Order);
    }

    private static ActionExecutingContext ContextFor(params object?[] arguments)
    {
        var services = new ServiceCollection()
            .AddSingleton<IValidator<Order>, OrderValidator>()
            .AddSingleton<IValidator<Circle>, CircleValidator>()
            .AddSingleton<IValidator<Lookup>, LookupValidator>()
            .BuildServiceProvider();

        var actionContext = new ActionContext(
            new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            [],
            arguments.Select((a, i) => (Key: $"arg{i}", Value: a)).ToDictionary(p => p.Key, p => p.Value),
            controller: new object());
    }

    private async Task<bool> Execute(ActionExecutingContext context)
    {
        var nextCalled = false;

        await _sut.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });

        return nextCalled;
    }

    private sealed record Order
    {
        public string Name { get; init; } = "";
        public List<Line> Lines { get; init; } = [];
    }

    private sealed record Line
    {
        public string Label { get; init; } = "";
    }

    private sealed class OrderValidator : AbstractValidator<Order>
    {
        public OrderValidator()
        {
            RuleFor(o => o.Name).NotEmpty().WithMessage("An order needs a name.");
            RuleForEach(o => o.Lines).ChildRules(line => line.RuleFor(l => l.Label).NotEmpty());
        }
    }

    private abstract record Shape;

    private sealed record Circle : Shape
    {
        public decimal Radius { get; init; }
    }

    private sealed class CircleValidator : AbstractValidator<Circle>
    {
        public CircleValidator() => RuleFor(c => c.Radius).GreaterThan(0);
    }

    private sealed record Unvalidated;

    private sealed record Lookup
    {
        public string Key { get; init; } = "";
    }

    private sealed class LookupValidator : AbstractValidator<Lookup>
    {
        public static CancellationToken LastToken { get; private set; }

        public LookupValidator() =>
            RuleFor(l => l.Key).MustAsync(async (key, cancellationToken) =>
            {
                LastToken = cancellationToken;
                await Task.Yield();
                return key != "taken";
            });
    }
}
