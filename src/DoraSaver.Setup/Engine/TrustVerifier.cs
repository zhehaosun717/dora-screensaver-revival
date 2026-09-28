using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace DoraSaver.Setup.Engine;

/// <summary>Checks that a downloaded installer carries a valid Authenticode signature from Microsoft.</summary>
internal static class TrustVerifier
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public static bool IsSignedByMicrosoft(string path)
    {
        if (!HasValidSignature(path))
        {
            return false;
        }

        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            string organization = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            return certificate.Subject.Split(',').Select(part => part.Trim()).Contains("O=Microsoft Corporation")
                && organization.StartsWith("Microsoft", StringComparison.Ordinal);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    private static bool HasValidSignature(string path)
    {
        IntPtr fileInfoPtr = IntPtr.Zero;
        IntPtr trustDataPtr = IntPtr.Zero;
        try
        {
            var fileInfo = new WinTrustFileInfo { StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>(), FilePath = path };
            fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

            var data = new WinTrustData
            {
                StructSize = (uint)Marshal.SizeOf<WinTrustData>(),
                UiChoice = 2, // WTD_UI_NONE
                RevocationChecks = 0, // WTD_REVOKE_NONE
                UnionChoice = 1, // WTD_CHOICE_FILE
                FileInfo = fileInfoPtr,
                StateAction = 0,
            };
            trustDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustData>());
            Marshal.StructureToPtr(data, trustDataPtr, false);

            Guid action = GenericVerifyV2;
            return WinVerifyTrust(new IntPtr(-1), ref action, trustDataPtr) == 0;
        }
        finally
        {
            if (fileInfoPtr != IntPtr.Zero)
            {
                Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPtr);
                Marshal.FreeHGlobal(fileInfoPtr);
            }

            if (trustDataPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(trustDataPtr);
            }
        }
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, IntPtr trustData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProvFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
