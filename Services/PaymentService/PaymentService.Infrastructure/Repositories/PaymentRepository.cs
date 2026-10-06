using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Data;

namespace PaymentService.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _context;

    public PaymentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Payment> CreateAsync(Payment payment)
    {
        _context.Payments.Add(payment);

        await _context.SaveChangesAsync();

        return payment;
    }

    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Payment>> GetByOrderIdAsync(int orderId)
    {
        return await _context.Payments
            .Where(p => p.OrderId == orderId)
            .ToListAsync();
    }

    public async Task<Payment?> UpdateStatusAsync(
        int id,
        PaymentStatus status)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return null;

        payment.Status = status;

        if (status == PaymentStatus.Successful)
        {
            payment.PaidAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return payment;
    }
}