namespace PoliedroHangFire.Domain.ClientBilling.Entities;
public class ClientBilling
{
    public int ClientBillingElectronicId { get; set; }
    public string? Name { get; set; }
    public bool Active { get; set; }
    public int Iterations { get; set; }
    public string? ApiKey { get; set; }
    public bool ResolutionType { get; set; }
}
