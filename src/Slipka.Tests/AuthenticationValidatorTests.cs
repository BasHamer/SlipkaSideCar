using Microsoft.IdentityModel.Tokens;
using Slipka.Configuration;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Slipka.Tests
{
    public class AuthenticationValidatorTests
    {
        private readonly AuthenticationSettings _validSettings;

        public AuthenticationValidatorTests()
        {
            _validSettings = new AuthenticationSettings
            {
                ValidateIssuer = true,
                Issuer = "test-issuer",
                ValidateAudience = true,
                Audience = "test-audience",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = "your-256-bit-secret-your-256-bit-secret",
                ClockSkewMinutes = 5
            };
        }

        [Fact]
        public void ValidateRequest_AuthenticationNotRequired_ReturnsValidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var request = new HttpRequestMessage();
            request.Headers.Add("Authorization", "Bearer some-token");

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: false);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(AuthenticationStatus.NotRequired, result.AuthenticationStatus);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void ValidateRequest_NoAuthorizationHeader_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var request = new HttpRequestMessage();

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: true);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(AuthenticationStatus.MissingToken, result.AuthenticationStatus);
            Assert.Contains("no JWT token found", result.ErrorMessage);
        }

        [Fact]
        public void ValidateRequest_ValidJwtToken_ReturnsValidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var token = GenerateValidJwtToken();
            var request = new HttpRequestMessage();
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: true);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(AuthenticationStatus.Authenticated, result.AuthenticationStatus);
            Assert.NotNull(result.ClaimsPrincipal);
            Assert.NotNull(result.JwtSecurityToken);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void ValidateRequest_JwtTokenWithoutBearerPrefix_ReturnsValidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var token = GenerateValidJwtToken();
            var request = new HttpRequestMessage();
            request.Headers.Add("Authorization", token);

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: true);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(AuthenticationStatus.Authenticated, result.AuthenticationStatus);
        }

        [Fact]
        public void ValidateRequest_ExpiredJwtToken_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var token = GenerateExpiredJwtToken();
            var request = new HttpRequestMessage();
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: true);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(AuthenticationStatus.TokenExpired, result.AuthenticationStatus);
            Assert.Contains("expired", result.ErrorMessage);
        }

        [Fact]
        public void ValidateRequest_InvalidSignature_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var token = GenerateJwtTokenWithWrongKey();
            var request = new HttpRequestMessage();
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: true);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(AuthenticationStatus.InvalidSignature, result.AuthenticationStatus);
            Assert.Contains("signature", result.ErrorMessage);
        }

        [Fact]
        public void ValidateRequest_InvalidJwtFormat_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var request = new HttpRequestMessage();
            request.Headers.Add("Authorization", "Bearer invalid.jwt.token");

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: true);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(AuthenticationStatus.InvalidToken, result.AuthenticationStatus);
            Assert.Contains("format", result.ErrorMessage);
        }

        [Fact]
        public void ValidateRequest_WrongIssuer_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var token = GenerateJwtTokenWithWrongIssuer();
            var request = new HttpRequestMessage();
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: true);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(AuthenticationStatus.InvalidToken, result.AuthenticationStatus);
            Assert.Contains("validation failed", result.ErrorMessage);
        }

        [Fact]
        public void ValidateRequest_WrongAudience_ReturnsInvalidResult()
        {
            // Arrange
            var validator = new AuthenticationValidator(_validSettings);
            var token = GenerateJwtTokenWithWrongAudience();
            var request = new HttpRequestMessage();
            request.Headers.Add("Authorization", $"Bearer {token}");

            // Act
            var result = validator.ValidateRequest(request, requiresAuthentication: true);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal(AuthenticationStatus.InvalidToken, result.AuthenticationStatus);
            Assert.Contains("validation failed", result.ErrorMessage);
        }

        [Fact]
        public void GetSecurityKey_EnvironmentVariableTakesPrecedence_ReturnsEnvironmentKey()
        {
            // Arrange
            const string envVarName = "TEST_JWT_KEY";
            const string envVarValue = "env-secret-key-256-bits-long-env-secret-key";
            const string configValue = "config-secret-key-256-bits-long-config-secret";

            Environment.SetEnvironmentVariable(envVarName, envVarValue);

            var settings = new AuthenticationSettings
            {
                IssuerSigningKey = configValue,
                IssuerSigningKeyEnvironmentVariable = envVarName
            };

            // Act
            var securityKey = settings.GetSecurityKey();

            // Assert
            Assert.NotNull(securityKey);
            var symmetricKey = Assert.IsType<SymmetricSecurityKey>(securityKey);
            var keyBytes = Encoding.UTF8.GetBytes(envVarValue);
            Assert.Equal(keyBytes, symmetricKey.Key);

            // Cleanup
            Environment.SetEnvironmentVariable(envVarName, null);
        }

        [Fact]
        public void GetSecurityKey_NoEnvironmentVariable_FallsBackToConfig_ReturnsConfigKey()
        {
            // Arrange
            const string envVarName = "NON_EXISTENT_ENV_VAR";
            const string configValue = "config-secret-key-256-bits-long-config-secret";

            // Ensure environment variable doesn't exist
            Environment.SetEnvironmentVariable(envVarName, null);

            var settings = new AuthenticationSettings
            {
                IssuerSigningKey = configValue,
                IssuerSigningKeyEnvironmentVariable = envVarName
            };

            // Act
            var securityKey = settings.GetSecurityKey();

            // Assert
            Assert.NotNull(securityKey);
            var symmetricKey = Assert.IsType<SymmetricSecurityKey>(securityKey);
            var keyBytes = Encoding.UTF8.GetBytes(configValue);
            Assert.Equal(keyBytes, symmetricKey.Key);
        }

        [Fact]
        public void GetSecurityKey_NoEnvironmentVariableOrConfig_ThrowsException()
        {
            // Arrange
            const string envVarName = "NON_EXISTENT_ENV_VAR";

            // Ensure environment variable doesn't exist
            Environment.SetEnvironmentVariable(envVarName, null);

            var settings = new AuthenticationSettings
            {
                IssuerSigningKey = "",
                IssuerSigningKeyEnvironmentVariable = envVarName
            };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => settings.GetSecurityKey());
            Assert.Contains("IssuerSigningKey is not configured", exception.Message);
            Assert.Contains(envVarName, exception.Message);
        }

        private string GenerateValidJwtToken()
        {
            var key = Encoding.UTF8.GetBytes("your-256-bit-secret-your-256-bit-secret");
            var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "test-issuer",
                audience: "test-audience",
                claims: new[] { new Claim("sub", "test-user") },
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateExpiredJwtToken()
        {
            var key = Encoding.UTF8.GetBytes("your-256-bit-secret-your-256-bit-secret");
            var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "test-issuer",
                audience: "test-audience",
                claims: new[] { new Claim("sub", "test-user") },
                expires: DateTime.UtcNow.AddMinutes(-1), // Already expired
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateJwtTokenWithWrongKey()
        {
            var wrongKey = Encoding.UTF8.GetBytes("wrong-256-bit-secret-wrong-256-bit-secret");
            var credentials = new SigningCredentials(new SymmetricSecurityKey(wrongKey), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "test-issuer",
                audience: "test-audience",
                claims: new[] { new Claim("sub", "test-user") },
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateJwtTokenWithWrongIssuer()
        {
            var key = Encoding.UTF8.GetBytes("your-256-bit-secret-your-256-bit-secret");
            var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "wrong-issuer",
                audience: "test-audience",
                claims: new[] { new Claim("sub", "test-user") },
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateJwtTokenWithWrongAudience()
        {
            var key = Encoding.UTF8.GetBytes("your-256-bit-secret-your-256-bit-secret");
            var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "test-issuer",
                audience: "wrong-audience",
                claims: new[] { new Claim("sub", "test-user") },
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
