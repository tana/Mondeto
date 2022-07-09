using System;
using System.Runtime.InteropServices;
using Microsoft.Quic;

namespace Mondeto.Core.QuicWrapper
{

unsafe class QuicConfiguration : IDisposable
{
    public QUIC_HANDLE* Handle;

    public QuicConfiguration(byte[][] alpns, bool datagramReceiveEnabled)
    {
        // Convert ALPNs
        QUIC_BUFFER* alpnBuffers = stackalloc QUIC_BUFFER[alpns.Length];
        for (int i = 0; i < alpns.Length; i++)
        {
            byte* buf = stackalloc byte[alpns[i].Length];
            Marshal.Copy(alpns[i], 0, (IntPtr)buf, alpns[i].Length);
            alpnBuffers[i].Length = (uint)alpns[i].Length;
            alpnBuffers[i].Buffer = buf;
        }

        // Prepare settings for creating configuration
        QUIC_SETTINGS settings = new();
        settings.IsSetFlags = 0;
        settings.DatagramReceiveEnabled = datagramReceiveEnabled ? (byte)1 : (byte)0;
        settings.IsSet.DatagramReceiveEnabled = 1;

        // Create configuration
        fixed (QUIC_HANDLE** handleAddr = &Handle)
        {
            MsQuic.ThrowIfFailure(
                QuicLibrary.ApiTable->ConfigurationOpen(
                    QuicLibrary.Registration,
                    alpnBuffers, (uint)alpns.Length,
                    &settings, (uint)sizeof(QUIC_SETTINGS),
                    null,   // context is null pointer
                    handleAddr
                )
            );
        }
    }

    public void SetCredentials(string privateKeyPath, string certificatePath)
    {
        fixed (byte* privateKeyCStr = QuicLibrary.ToCString(privateKeyPath))
        fixed (byte* certificateCStr = QuicLibrary.ToCString(certificatePath))
        {
            QUIC_CERTIFICATE_FILE certFile = new();
            certFile.PrivateKeyFile = (sbyte*)privateKeyCStr;
            certFile.CertificateFile = (sbyte*)certificateCStr;
            QUIC_CREDENTIAL_CONFIG credConfig = new();
            credConfig.Type = QUIC_CREDENTIAL_TYPE.QUIC_CREDENTIAL_TYPE_CERTIFICATE_FILE;
            credConfig.CertificateFile = &certFile;

            // Load TLS credentials
            MsQuic.ThrowIfFailure(
                QuicLibrary.ApiTable->ConfigurationLoadCredential(Handle, &credConfig)
            );
        }
    }
    
    public void Dispose()
    {
        if (Handle != null)
        {
            QuicLibrary.ApiTable->ConfigurationClose(Handle);
        }
    }
}

}   // end namespace