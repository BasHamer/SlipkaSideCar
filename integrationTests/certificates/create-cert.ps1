# Create self-signed certificate for HTTPS testing
$cert = New-SelfSignedCertificate -DnsName 'localhost' -CertStoreLocation 'cert:\LocalMachine\My' -KeyExportPolicy Exportable -KeySpec Signature
$certPath = 'localhost.pfx'
$password = ConvertTo-SecureString -String 'testpassword' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $password
Write-Host "Certificate created at: $certPath"

# Also export as CER for trust store
$cerPath = 'localhost.cer'
Export-Certificate -Cert $cert -FilePath $cerPath
Write-Host "Certificate exported at: $cerPath"
