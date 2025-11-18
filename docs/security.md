# Security & Authentication

Slipka implements comprehensive security measures including JWT-based authentication, secure configuration management, and protection against common web vulnerabilities.

## Overview

Security is implemented at multiple layers including API authentication, data encryption, secure headers, and access control to ensure safe operation in testing and production environments.

## Authentication Configuration

Authentication settings are configured in `appsettings.json`:

```json
{
  "Authentication": {
    "ValidateIssuer": true,
    "Issuer": "slipka-proxy",
    "ValidateAudience": true,
    "Audience": "slipka-api",
    "ValidateLifetime": true,
    "ValidateIssuerSigningKey": true,
    "IssuerSigningKey": "",
    "IssuerSigningKeyEnvironmentVariable": "SLIPKA_JWT_SIGNING_KEY",
    "ClockSkewMinutes": 5
  }
}
```

## JWT Token Usage

### Obtaining Tokens

```bash
# Generate JWT token (implementation depends on your auth service)
curl -X POST https://auth-service.com/token \
  -H "Content-Type: application/json" \
  -d '{
    "client_id": "slipka-client",
    "client_secret": "your-secret",
    "audience": "slipka-api",
    "grant_type": "client_credentials"
  }'
```

### Using Tokens in Requests

```bash
curl -X POST http://localhost:8080/api/Proxies \
  -H "Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "Name": "secure-proxy",
    "TargetHost": "api.example.com",
    "TargetPort": 443,
    "TargetPortHttps": true
  }'
```

## API Security

### Route-Level Authentication

Configure authentication requirements for reverse proxy routes:

```json
{
  "ReverseProxy": {
    "Routes": [
      {
        "Id": "public-api",
        "Path": "/api/public/*",
        "TargetHost": "public-service",
        "RequiresAuthentication": false
      },
      {
        "Id": "private-api",
        "Path": "/api/private/*",
        "TargetHost": "private-service",
        "RequiresAuthentication": true
      }
    ]
  }
}
```

### Secure Headers

Slipka automatically adds security headers to proxied responses:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Strict-Transport-Security: max-age=31536000`

## Data Protection

### Encryption at Rest

Sensitive data is encrypted using configured encryption keys:

```json
{
  "Encryption": {
    "Key": "your-encryption-key-here",
    "Algorithm": "AES256"
  }
}
```

### Secure Communication

All internal communications use HTTPS/TLS:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://localhost:8080",
        "Certificate": {
          "Path": "certs/slipka.pfx",
          "Password": "certificate-password"
        }
      }
    }
  }
}
```

## Access Control

### Role-Based Access

Implement role-based access control for different operations:

```json
{
  "Authorization": {
    "Roles": {
      "Admin": ["create-proxy", "delete-proxy", "manage-static"],
      "Tester": ["create-proxy", "read-sessions"],
      "Viewer": ["read-sessions", "read-health"]
    }
  }
}
```

### IP Whitelisting

Restrict access to specific IP ranges:

```json
{
  "Security": {
    "AllowedIPs": ["192.168.1.0/24", "10.0.0.0/8"],
    "BlockedIPs": ["192.168.1.100"]
  }
}
```

## Secure Configuration

### Environment Variables

Store sensitive configuration in environment variables:

```bash
export SLIPKA_JWT_SIGNING_KEY="your-256-bit-secret"
export MONGODB_CONNECTION_STRING="mongodb://user:password@host:port/db"
export REDIS_CONNECTION_STRING="redis://user:password@host:port"
```

### Secret Management

Integrate with secret management systems:

```json
{
  "Secrets": {
    "Provider": "AzureKeyVault",
    "VaultUrl": "https://myvault.vault.azure.net/",
    "ClientId": "client-id",
    "ClientSecret": "client-secret"
  }
}
```

## Security Best Practices

### 1. Authentication
- Always use JWT tokens for API access
- Implement token rotation policies
- Use strong signing keys (256-bit minimum)

### 2. Authorization
- Apply principle of least privilege
- Regularly audit access permissions
- Implement role-based access control

### 3. Data Protection
- Encrypt sensitive data at rest
- Use HTTPS for all communications
- Implement proper certificate management

### 4. Monitoring
- Log security events and failures
- Monitor for suspicious activity
- Implement alerting for security incidents

### 5. Compliance
- Regular security audits and penetration testing
- Implement data retention policies
- Follow industry security standards (OWASP, etc.)

## Security Monitoring

### Failed Authentication Logging

```bash
curl -X POST http://localhost:8080/api/logging \
  -H "Content-Type: application/json" \
  -d '{
    "CorrelationId": "sec-12345",
    "Level": "Warning",
    "Message": "Authentication failed",
    "Properties": {
      "IPAddress": "192.168.1.100",
      "UserAgent": "Suspicious-Client/1.0",
      "Reason": "Invalid token"
    }
  }'
```

### Security Event Correlation

```bash
# Query security events
curl -X GET "http://localhost:8080/api/Sessions/session_123/calls?tags=security-event"
```

## Incident Response

### Security Incident Procedure

1. **Detection**: Monitor security logs and alerts
2. **Containment**: Disable compromised accounts/tokens
3. **Investigation**: Analyze logs and traffic patterns
4. **Recovery**: Restore from clean backups
5. **Lessons Learned**: Update security measures

### Emergency Access

```bash
# Emergency shutdown
curl -X POST http://localhost:8080/api/admin/shutdown \
  -H "Authorization: Bearer emergency-token"

# Force cleanup
curl -X POST http://localhost:8080/api/admin/cleanup \
  -H "Authorization: Bearer emergency-token"
```

## Compliance Considerations

### GDPR Compliance

- Implement data minimization principles
- Provide data deletion capabilities
- Document data processing activities
- Implement audit logging for data access

### SOC 2 Compliance

- Regular security assessments
- Change management procedures
- Incident response planning
- Continuous monitoring and alerting

### Industry Standards

- OWASP security headers implementation
- Regular vulnerability scanning
- Secure coding practices
- Third-party component updates
