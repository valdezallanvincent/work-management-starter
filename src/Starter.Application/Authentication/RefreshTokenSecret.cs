using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Starter.Application.Authentication;

/// <summary>
/// Refresh tokens are opaque random values. Only a SHA-256 hash is stored, so a database leak
/// does not yield usable tokens. A fast hash is appropriate here because the input has 256 bits
/// of entropy; slow password hashes exist to protect low-entropy secrets.
/// </summary>
public static class RefreshTokenSecret
{
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
