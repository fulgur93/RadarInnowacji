using RadarInnowacji.Core.Entities;
using RadarInnowacji.Core.Models;

namespace RadarInnowacji.Core.Interfaces;

/// <summary>Katalog odbiorców, partnerów i inwestorów.</summary>
public interface IRecipientService
{
    Task<IReadOnlyList<Recipient>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> SaveAsync(RecipientForm form, CancellationToken cancellationToken = default);
    Task<OperationResult> SetActiveAsync(int recipientId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Dodaje przykładowe typy odbiorców, gdy katalog jest pusty. Zwraca liczbę dodanych wpisów.</summary>
    Task<int> SeedExamplesAsync(CancellationToken cancellationToken = default);
}
