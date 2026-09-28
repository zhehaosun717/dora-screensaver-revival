using System.Security.Cryptography;

namespace DoraSaver.Setup.Engine;

/// <summary>
/// The old installers embed the Flash movie as-is inside their own .exe. Rather than running those
/// installers, look for every SWF signature ("FWS"/"CWS") and keep the slice whose length and
/// SHA-256 match the catalog, so only the exact original movie can come out.
/// </summary>
internal static class SwfCarver
{
    public static byte[]? Find(IEnumerable<byte[]> blobs, int length, string sha256)
    {
        using var sha = SHA256.Create();
        foreach (byte[] blob in blobs)
        {
            for (int i = 0; i + length <= blob.Length; i++)
            {
                if (!IsSignature(blob, i))
                {
                    continue;
                }

                if (Hash.Hex(sha.ComputeHash(blob, i, length)) == sha256)
                {
                    var result = new byte[length];
                    Buffer.BlockCopy(blob, i, result, 0, length);
                    return result;
                }
            }
        }

        return null;
    }

    internal static bool IsSignature(byte[] data, int offset)
    {
        byte first = data[offset];
        return (first == (byte)'F' || first == (byte)'C')
            && data[offset + 1] == (byte)'W'
            && data[offset + 2] == (byte)'S'
            && data[offset + 3] is >= 1 and <= 40;
    }
}

internal static class Hash
{
    public static string Hex(byte[] digest) => BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();

    public static string Sha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return Hex(sha.ComputeHash(data));
    }

    public static string Sha256File(string path)
    {
        using var sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return Hex(sha.ComputeHash(stream));
    }
}
