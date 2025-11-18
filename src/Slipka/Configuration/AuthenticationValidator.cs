using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;

namespace Slipka.Configuration
{
    /// <summary>
    /// Base validator for authentication verification with JWT token support
    /// </summary>
    public class AuthenticationValidator
    {
        private readonly AuthenticationSettings _settings;

        public AuthenticationValidator(AuthenticationSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Validates authentication for an HTTP request
        /// </summary>
        /// <param name="request">The HTTP request to validate</param>
        /// <param name="requiresAuthentication">Whether authentication is required for this request</param>
        /// <returns>Validation result indicating success or failure with details</returns>
        public AuthenticationValidationResult ValidateRequest(HttpRequestMessage request, bool requiresAuthentication)
        {
            var result = new AuthenticationValidationResult();

            // If authentication is not required, allow the request
            if (!requiresAuthentication)
            {
                result.IsValid = true;
                result.AuthenticationStatus = AuthenticationStatus.NotRequired;
                return result;
            }

            // Authentication is required - check for JWT token
            var token = ExtractJwtToken(request);

            if (string.IsNullOrEmpty(token))
            {
                result.IsValid = false;
                result.AuthenticationStatus = AuthenticationStatus.MissingToken;
                result.ErrorMessage = "Authentication required but no JWT token found";
                return result;
            }

            // Validate the JWT token
            return ValidateJwtToken(token);
        }

        /// <summary>
        /// Extracts JWT token from Authorization header
        /// </summary>
        private string ExtractJwtToken(HttpRequestMessage request)
        {
            if (!request.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                return null;
            }

            var authHeader = authHeaders.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                return null;
            }

            // Support both "Bearer <token>" and "<token>" formats
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring(7).Trim();
            }

            // If it doesn't start with "Bearer ", assume the whole header is the token
            return authHeader.Trim();
        }

        /// <summary>
        /// Validates a JWT token
        /// </summary>
        private AuthenticationValidationResult ValidateJwtToken(string token)
        {
            var result = new AuthenticationValidationResult();

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();

                // Check if the token format is valid
                if (!tokenHandler.CanReadToken(token))
                {
                    result.IsValid = false;
                    result.AuthenticationStatus = AuthenticationStatus.InvalidToken;
                    result.ErrorMessage = "Invalid JWT token format";
                    return result;
                }

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = _settings.ValidateIssuer,
                    ValidIssuer = _settings.Issuer,

                    ValidateAudience = _settings.ValidateAudience,
                    ValidAudience = _settings.Audience,

                    ValidateLifetime = _settings.ValidateLifetime,

                ValidateIssuerSigningKey = _settings.ValidateIssuerSigningKey,
                IssuerSigningKey = _settings.GetSecurityKey(),

                    ClockSkew = TimeSpan.FromMinutes(_settings.ClockSkewMinutes)
                };

                // Validate the token
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                result.IsValid = true;
                result.AuthenticationStatus = AuthenticationStatus.Authenticated;
                result.ClaimsPrincipal = principal;
                result.JwtSecurityToken = validatedToken as JwtSecurityToken;

                return result;
            }
            catch (SecurityTokenExpiredException)
            {
                result.IsValid = false;
                result.AuthenticationStatus = AuthenticationStatus.TokenExpired;
                result.ErrorMessage = "JWT token has expired";
                return result;
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                result.IsValid = false;
                result.AuthenticationStatus = AuthenticationStatus.InvalidSignature;
                result.ErrorMessage = "JWT token has invalid signature";
                return result;
            }
            catch (SecurityTokenException ex)
            {
                result.IsValid = false;
                result.AuthenticationStatus = AuthenticationStatus.InvalidToken;
                result.ErrorMessage = $"JWT token validation failed: {ex.Message}";
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.AuthenticationStatus = AuthenticationStatus.ValidationError;
                result.ErrorMessage = $"Authentication validation error: {ex.Message}";
                return result;
            }
        }
    }

    /// <summary>
    /// Authentication settings for JWT validation
    /// </summary>
    public class AuthenticationSettings
    {
        /// <summary>
        /// Whether to validate the token issuer
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Expected issuer for JWT tokens
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Whether to validate the token audience
        /// </summary>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// Expected audience for JWT tokens
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// Whether to validate token lifetime
        /// </summary>
        public bool ValidateLifetime { get; set; } = true;

        /// <summary>
        /// Whether to validate the issuer signing key
        /// </summary>
        public bool ValidateIssuerSigningKey { get; set; } = true;

        /// <summary>
        /// The signing key for validating JWT tokens (can be a base64 string or plain text)
        /// </summary>
        public string IssuerSigningKey { get; set; }

        /// <summary>
        /// The environment variable name to read the signing key from (takes precedence over IssuerSigningKey)
        /// </summary>
        public string IssuerSigningKeyEnvironmentVariable { get; set; } = "SLIPKA_JWT_SIGNING_KEY";

        /// <summary>
        /// Clock skew in minutes for token lifetime validation
        /// </summary>
        public int ClockSkewMinutes { get; set; } = 5;

        /// <summary>
        /// Gets the security key for JWT validation
        /// </summary>
        public SecurityKey GetSecurityKey()
        {
            // First, try to get the key from environment variable
            string keyValue = null;
            if (!string.IsNullOrEmpty(IssuerSigningKeyEnvironmentVariable))
            {
                keyValue = Environment.GetEnvironmentVariable(IssuerSigningKeyEnvironmentVariable);
            }

            // If environment variable is not set or empty, fall back to configured value
            if (string.IsNullOrEmpty(keyValue))
            {
                keyValue = IssuerSigningKey;
            }

            if (string.IsNullOrEmpty(keyValue))
            {
                throw new InvalidOperationException(
                    $"IssuerSigningKey is not configured. Set the '{IssuerSigningKeyEnvironmentVariable}' environment variable or configure IssuerSigningKey in settings.");
            }

            // Try to decode as base64 first, fall back to plain text
            try
            {
                var keyBytes = Convert.FromBase64String(keyValue);
                return new SymmetricSecurityKey(keyBytes);
            }
            catch (FormatException)
            {
                // If it's not valid base64, treat it as plain text
                var keyBytes = Encoding.UTF8.GetBytes(keyValue);
                return new SymmetricSecurityKey(keyBytes);
            }
        }

        /// <summary>
        /// Creates a symmetric security key from a base64-encoded secret
        /// </summary>
        public static SecurityKey CreateSymmetricSecurityKey(string secret)
        {
            var keyBytes = Convert.FromBase64String(secret);
            return new SymmetricSecurityKey(keyBytes);
        }

        /// <summary>
        /// Creates a symmetric security key from a plain text secret
        /// </summary>
        public static SecurityKey CreateSymmetricSecurityKeyFromText(string secret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            return new SymmetricSecurityKey(keyBytes);
        }
    }

    /// <summary>
    /// Result of authentication validation
    /// </summary>
    public class AuthenticationValidationResult
    {
        /// <summary>
        /// Whether the authentication validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// The authentication status
        /// </summary>
        public AuthenticationStatus AuthenticationStatus { get; set; }

        /// <summary>
        /// Error message if validation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The claims principal if authentication succeeded
        /// </summary>
        public ClaimsPrincipal ClaimsPrincipal { get; set; }

        /// <summary>
        /// The validated JWT security token
        /// </summary>
        public JwtSecurityToken JwtSecurityToken { get; set; }
    }

    /// <summary>
    /// Authentication status enumeration
    /// </summary>
    public enum AuthenticationStatus
    {
        /// <summary>
        /// Authentication was successful
        /// </summary>
        Authenticated,

        /// <summary>
        /// Authentication was not required for this request
        /// </summary>
        NotRequired,

        /// <summary>
        /// No authentication token was provided
        /// </summary>
        MissingToken,

        /// <summary>
        /// The JWT token has expired
        /// </summary>
        TokenExpired,

        /// <summary>
        /// The JWT token has an invalid signature
        /// </summary>
        InvalidSignature,

        /// <summary>
        /// The JWT token format is invalid
        /// </summary>
        InvalidToken,

        /// <summary>
        /// An error occurred during token validation
        /// </summary>
        ValidationError
    }
}
