# Test Certificates

This directory contains self-signed certificates used for HTTPS integration testing.

## Files

- `localhost.pfx` - PKCS#12 certificate file containing the private key and certificate
- `localhost.cer` - Certificate file (public key only) for certificate trust store
- `create-cert.ps1` - PowerShell script to regenerate certificates (Windows only)
- `CertificateGenerator.csproj` & `Program.cs` - .NET program to generate certificates programmatically

## Certificate Details

- **Subject**: CN=localhost
- **Subject Alternative Name**: localhost
- **Valid From**: 1 day ago (to avoid clock skew issues)
- **Valid To**: 1 year from creation
- **Password**: `testpassword`
- **Key Usage**: Digital Signature, Key Encipherment
- **Enhanced Key Usage**: Server Authentication

## Usage

The certificates are automatically mounted into the Slipka Docker container at `/app/certificates/` and configured via environment variables:

```yaml
environment:
  - Kestrel__Certificates__Default__Path=/app/certificates/localhost.pfx
  - Kestrel__Certificates__Default__Password=testpassword
volumes:
  - ./certificates:/app/certificates:ro
```

## Regenerating Certificates

If you need to regenerate the certificates:

### Using .NET (Cross-platform)
```bash
cd integrationTests/certificates
dotnet run
```

### Using PowerShell (Windows)
```powershell
.\create-cert.ps1
```

## Security Notes

⚠️ **These certificates are for testing only and should never be used in production!**

- The certificates are self-signed and will not be trusted by browsers or clients by default
- The private key password is hardcoded for testing convenience
- Certificate validation in tests uses custom callbacks that accept self-signed certificates

For production use, obtain certificates from a trusted Certificate Authority (CA) and use proper certificate management practices.
