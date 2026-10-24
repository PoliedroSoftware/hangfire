using DianResolutionEntity = PoliedroHangFire.Domain.DianResolution.Entities.DianResolution;

namespace PoliedroHangFire.Application.DianResolution.Interfaces;
public interface IDianResolutionService
{
    Task<List<DianResolutionEntity>> GetActiveResolutionsAsync();
}
