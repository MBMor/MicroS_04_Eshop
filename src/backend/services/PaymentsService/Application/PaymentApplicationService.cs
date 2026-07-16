using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentsService.Data;
using PaymentsService.Domain;

namespace PaymentsService.Application;

public sealed class PaymentApplicationService(
    PaymentsDbContext dbContext,
    FakePaymentProcessor fakePaymentProcessor,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<Payment>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Payments
            .AsNoTracking()
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<Payment?> GetByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        return dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                payment => payment.Id == paymentId,
                cancellationToken);
    }

    public Task<Payment?> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        return dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                payment => payment.OrderId == orderId,
                cancellationToken);
    }

    public async Task<CreatePaymentResult> CreateAndProcessAsync(
        Guid orderId,
        string customerId,
        decimal amount,
        string currency,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        if (!fakePaymentProcessor.TryProcess(
                paymentMethod,
                out FakePaymentDecision? decision)
            || decision is null)
        {
            return CreatePaymentResult.ValidationFailed(
                $"Unsupported fake payment method. Supported values are " +
                $"'{FakePaymentProcessor.SuccessMethod}' and " +
                $"'{FakePaymentProcessor.FailureMethod}'.");
        }

        bool paymentAlreadyExists =
            await dbContext.Payments.AnyAsync(
                payment => payment.OrderId == orderId,
                cancellationToken);

        if (paymentAlreadyExists)
        {
            return CreatePaymentResult.Conflict(
                $"A payment for order '{orderId}' already exists.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        Payment payment;

        try
        {
            payment = Payment.CreatePending(
                Guid.NewGuid(),
                orderId,
                customerId,
                amount,
                currency,
                decision.PaymentMethod,
                now);

            if (decision.IsAuthorized)
            {
                payment.Authorize(now);
            }
            else
            {
                payment.Fail(
                    decision.FailureReason
                    ?? "Simulated payment failure.",
                    now);
            }
        }
        catch (ArgumentException exception)
        {
            return CreatePaymentResult.ValidationFailed(
                exception.Message);
        }

        dbContext.Payments.Add(payment);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            return CreatePaymentResult.Conflict(
                $"A payment for order '{orderId}' already exists.");
        }

        return CreatePaymentResult.Succeeded(payment);
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }
}
