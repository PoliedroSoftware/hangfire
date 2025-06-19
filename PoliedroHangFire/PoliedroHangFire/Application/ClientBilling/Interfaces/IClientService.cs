namespace PoliedroHangFire.Application.ClientBilling.Interfaces;
using PoliedroHangFire.Domain.ClientBilling.Entities;
public interface IClientService
{
    Task<List<ClientBilling>> GetClientBillingsAsync();
}
