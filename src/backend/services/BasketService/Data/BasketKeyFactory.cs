using System.Security.Cryptography;
using System.Text;

namespace BasketService.Data;

public static class BasketKeyFactory
{
    public static string Create(string customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        byte[] customerIdBytes = Encoding.UTF8.GetBytes(customerId);
        byte[] hash = SHA256.HashData(customerIdBytes);

        return $"basket:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
