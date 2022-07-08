using Microsoft.Quic;

namespace Mondeto.Core.QuicWrapper
{

// This class hides unsafe things related to MsQuic
unsafe class QuicLibrary
{
    public static QUIC_API_TABLE* ApiTable = null;
    public static QUIC_HANDLE* Registration = null;

    public static bool IsReady { get => ApiTable != null; }

    // Initialize MsQuic library
    public static void Initialize()
    {
        ApiTable = MsQuic.Open();

        // Create a registration with default config
        fixed (QUIC_HANDLE** registrationAddr = &Registration)
        {
            MsQuic.ThrowIfFailure(
                ApiTable->RegistrationOpen(null, registrationAddr)
            );
        }
    }

    public static void Close()
    {
        if (ApiTable != null)
        {
            if (Registration != null)
            {
                ApiTable->RegistrationClose(Registration);
            }

            MsQuic.Close(ApiTable);
        }
    }
}

}   // end namespace