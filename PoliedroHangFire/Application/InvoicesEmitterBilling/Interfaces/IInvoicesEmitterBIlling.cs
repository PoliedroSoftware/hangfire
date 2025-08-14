namespace PoliedroHangFire.Application.InvoicesEmitterBilling.Interfaces;
public interface IInvoicesEmitterBIlling
{
    Task InvoicesEmitterServicesAsync(string jsonInvoices, string token);
}
