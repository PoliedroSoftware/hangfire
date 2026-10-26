namespace PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
public interface IPendingInvoicesBilling
{
    Task InvoicePendingAsync(int resolutionId, string displayName);
}
