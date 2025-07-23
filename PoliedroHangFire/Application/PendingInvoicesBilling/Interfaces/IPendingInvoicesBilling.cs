namespace PoliedroHangFire.Application.PendingInvoicesBilling.Interfaces;
public interface IPendingInvoicesBilling
{
    Task InvoicePendingAsync(int clienteId, string token, bool resolutionType, string name);
}
