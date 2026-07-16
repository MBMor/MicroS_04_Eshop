using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PaymentsService.Application;
using PaymentsService.Contracts;
using PaymentsService.Domain;

namespace PaymentsService.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/payments")]
public sealed class PaymentsController(
    PaymentApplicationService paymentService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentResponse>>>
        GetPayments(
            CancellationToken cancellationToken)
    {
        IReadOnlyList<Payment> payments =
            await paymentService.ListAsync(
                cancellationToken);

        PaymentResponse[] response = payments
            .Select(PaymentResponse.FromPayment)
            .ToArray();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentResponse>>
        GetPaymentById(
            Guid id,
            CancellationToken cancellationToken)
    {
        Payment? payment =
            await paymentService.GetByIdAsync(
                id,
                cancellationToken);

        if (payment is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Payment was not found.",
                $"Payment '{id}' does not exist."));
        }

        return Ok(PaymentResponse.FromPayment(payment));
    }

    [HttpGet("by-order/{orderId:guid}")]
    public async Task<ActionResult<PaymentResponse>>
        GetPaymentByOrderId(
            Guid orderId,
            CancellationToken cancellationToken)
    {
        Payment? payment =
            await paymentService.GetByOrderIdAsync(
                orderId,
                cancellationToken);

        if (payment is null)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Payment was not found.",
                $"No payment exists for order '{orderId}'."));
        }

        return Ok(PaymentResponse.FromPayment(payment));
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>>
        CreatePayment(
            CreatePaymentRequest request,
            CancellationToken cancellationToken)
    {
        if (request.OrderId == Guid.Empty)
        {
            ModelState.AddModelError(
                nameof(request.OrderId),
                "OrderId must not be empty.");

            return ValidationProblem(ModelState);
        }

        CreatePaymentResult result =
            await paymentService.CreateAndProcessAsync(
                request.OrderId,
                request.CustomerId,
                request.Amount,
                request.Currency,
                request.PaymentMethod,
                cancellationToken);

        if (result.Status == CreatePaymentStatus.Success
            && result.Payment is not null)
        {
            PaymentResponse response =
                PaymentResponse.FromPayment(result.Payment);

            return CreatedAtAction(
                nameof(GetPaymentById),
                new
                {
                    version = RouteData.Values["version"],
                    id = result.Payment.Id
                },
                response);
        }

        return result.Status switch
        {
            CreatePaymentStatus.Conflict
                => Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Payment conflict.",
                    result.Error)),

            CreatePaymentStatus.ValidationFailed
                => BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Payment validation failed.",
                    result.Error)),

            _ => throw new InvalidOperationException(
                $"Unexpected payment result '{result.Status}'.")
        };
    }

    private static ProblemDetails CreateProblem(
        int status,
        string title,
        string? detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
