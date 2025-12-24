using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using App.Application.Common.Exceptions;
using App.Application.Common.Interfaces;
using App.Application.Common.Models;
using App.Application.Common.Security;
using App.Application.Common.Utils;
using App.Domain.Entities;
using CSharpVitamins;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace App.Application.Login.Commands;

public class LoginWithJwt
{
    public record Command : LoggableRequest<CommandResponseDto<LoginDto>>
    {
        [JsonIgnore]
        public string Token { get; init; } = null!;
        public string DeveloperName { get; init; } = null!;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IAppDbContext db)
        {
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.DeveloperName).NotEmpty();
            RuleFor(x => x)
                .CustomAsync(
                    async (request, context, cancellationToken) =>
                    {
                        var developerName = request.DeveloperName.ToDeveloperName();
                        var authScheme = await db
                            .AuthenticationSchemes.AsNoTracking()
                            .FirstOrDefaultAsync(
                                p => p.DeveloperName == developerName,
                                cancellationToken
                            );

                        if (authScheme == null)
                        {
                            throw new NotFoundException(
                                "Auth scheme",
                                $"{request.DeveloperName} was not found"
                            );
                        }

                        if (!authScheme.IsEnabledForUsers && !authScheme.IsEnabledForAdmins)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Authentication scheme is disabled"
                            );
                            return;
                        }

                        JwtPayload payload;
                        try
                        {
                            var validationParameters = new TokenValidationParameters()
                            {
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey = new SymmetricSecurityKey(
                                    Encoding.ASCII.GetBytes(
                                        authScheme.JwtSecretKey.PadRight((256 / 8), '\0')
                                    )
                                ),
                                ValidateIssuer = false,
                                ValidateAudience = false,
                                ClockSkew = TimeSpan.Zero,
                            };
                            SecurityToken decodedToken;
                            new JwtSecurityTokenHandler().ValidateToken(
                                request.Token,
                                validationParameters,
                                out decodedToken
                            );

                            payload = ((JwtSecurityToken)decodedToken).Payload;
                        }
                        catch (SecurityTokenExpiredException)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Security token has expired."
                            );
                            return;
                        }
                        catch (Exception)
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                "Invalid security token."
                            );
                            return;
                        }

                        if (authScheme.JwtUseHighSecurity)
                        {
                            var jti = payload.Jti;
                            if (string.IsNullOrEmpty(jti))
                            {
                                context.AddFailure(
                                    Constants.VALIDATION_SUMMARY,
                                    "JWT high security enabled: 'jti' attribute is required in security token."
                                );
                                return;
                            }

                            var jtiResult = await db
                                .JwtLogins.AsNoTracking()
                                .FirstOrDefaultAsync(p => p.Jti == jti, cancellationToken);
                            if (jtiResult != null)
                            {
                                context.AddFailure(
                                    Constants.VALIDATION_SUMMARY,
                                    $"Security token already consumed: {jtiResult.Jti}"
                                );
                                return;
                            }
                        }

                        string? email =
                            payload.GetValueOrDefault<string, object>(JwtRegisteredClaimNames.Email)
                            as string;

                        if (!payload.ContainsKey(JwtRegisteredClaimNames.Email))
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                $"'email' is missing from security token"
                            );
                            return;
                        }

                        email = email!.ToLower().Trim();

                        if (!email.IsValidEmailAddress())
                        {
                            context.AddFailure(
                                Constants.VALIDATION_SUMMARY,
                                $"'email' is not a valid email address"
                            );
                            return;
                        }

                        User? entity = null;
                        var sub = payload.Sub;
                        if (payload.ContainsKey(JwtRegisteredClaimNames.Sub))
                        {
                            entity = await db
                                .Users.AsNoTracking()
                                .FirstOrDefaultAsync(
                                    p =>
                                        p.SsoId == sub && p.AuthenticationSchemeId == authScheme.Id,
                                    cancellationToken
                                );
                        }
                        else
                        {
                            entity = await db
                                .Users.AsNoTracking()
                                .FirstOrDefaultAsync(
                                    p => p.EmailAddress.ToLower() == email,
                                    cancellationToken
                                );
                        }

                        if (entity != null)
                        {
                            if (entity.IsAdmin && !authScheme.IsEnabledForAdmins)
                            {
                                context.AddFailure(
                                    Constants.VALIDATION_SUMMARY,
                                    $"Authentication scheme disabled for administrators."
                                );
                                return;
                            }

                            if (!entity.IsAdmin && !authScheme.IsEnabledForUsers)
                            {
                                context.AddFailure(
                                    Constants.VALIDATION_SUMMARY,
                                    $"Authentication scheme disabled for public users."
                                );
                                return;
                            }

                            if (!entity.IsActive)
                            {
                                context.AddFailure(
                                    Constants.VALIDATION_SUMMARY,
                                    $"User has been deactivated."
                                );
                                return;
                            }
                        }
                    }
                );
        }
    }

    public class Handler : IRequestHandler<Command, CommandResponseDto<LoginDto>>
    {
        private readonly IAppDbContext _db;

        public Handler(IAppDbContext db)
        {
            _db = db;
        }

        public async ValueTask<CommandResponseDto<LoginDto>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var authScheme = await _db.AuthenticationSchemes.FirstAsync(
                p => p.DeveloperName == request.DeveloperName.ToDeveloperName(),
                cancellationToken
            );

            var validationParameters = new TokenValidationParameters()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.ASCII.GetBytes(authScheme.JwtSecretKey.PadRight((256 / 8), '\0'))
                ),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero,
            };
            SecurityToken decodedToken;
            var claimsPrincipal = new JwtSecurityTokenHandler().ValidateToken(
                request.Token,
                validationParameters,
                out decodedToken
            );
            var payload = ((JwtSecurityToken)decodedToken).Payload;

            JwtLogin? jwtLogin = null;
            if (authScheme.JwtUseHighSecurity)
            {
                var jti = payload.Jti;
                jwtLogin = new JwtLogin { Id = Guid.NewGuid(), Jti = jti };
            }

            string sub = payload.Sub;
            string? email =
                payload.GetValueOrDefault<string, object>(JwtRegisteredClaimNames.Email) as string;
            string? givenName =
                payload.GetValueOrDefault<string, object>(JwtRegisteredClaimNames.GivenName)
                as string;
            string? familyName =
                payload.GetValueOrDefault<string, object>(JwtRegisteredClaimNames.FamilyName)
                as string;
            var userGroupsList = payload
                .Claims.Where(p => p.Type == AppClaimTypes.UserGroups)
                .Select(p => p.Value)
                .ToList();

            User? entity = null;

            if (!string.IsNullOrWhiteSpace(sub))
            {
                entity = await _db
                    .Users.Include(p => p.UserGroups)
                    .FirstOrDefaultAsync(
                        p => p.SsoId == sub && p.AuthenticationSchemeId == authScheme.Id,
                        cancellationToken
                    );
            }

            if (entity == null)
            {
                var cleanedEmail = email!.Trim().ToLower();
                entity = await _db
                    .Users.Include(p => p.UserGroups)
                    .FirstOrDefaultAsync(
                        p => p.EmailAddress.ToLower() == cleanedEmail,
                        cancellationToken
                    );
            }

            ICollection<UserGroup>? foundUserGroups = null;
            if (userGroupsList.Count > 0)
            {
                foundUserGroups = await _db
                    .UserGroups.Where(p => userGroupsList.Any(c => c == p.DeveloperName))
                    .ToListAsync(cancellationToken);
            }

            //no user found at all, create a new user on the fly
            bool firstTime = false;
            if (entity == null)
            {
                firstTime = true;
                var id = Guid.NewGuid();
                ShortGuid shortGuid = id;
                var salt = PasswordUtility.RandomSalt();
                entity = new User
                {
                    Id = id,
                    EmailAddress = email.Trim(),
                    FirstName = givenName.IfNullOrEmpty("SsoVisitor"),
                    LastName = familyName.IfNullOrEmpty(shortGuid),
                    IsActive = true,
                    Salt = salt,
                    PasswordHash = PasswordUtility.Hash(PasswordUtility.RandomPassword(12), salt),
                    SsoId = sub,
                    UserGroups = foundUserGroups,
                };
            }
            else
            {
                entity.SsoId = sub.IfNullOrEmpty(entity.SsoId);
                entity.FirstName = givenName.IfNullOrEmpty(entity.FirstName);
                entity.LastName = familyName.IfNullOrEmpty(entity.LastName);
                entity.EmailAddress = email.Trim();

                if (foundUserGroups != null && foundUserGroups.Count > 0)
                {
                    foreach (var ug in entity.UserGroups)
                    {
                        if (!foundUserGroups.Any(p => p.DeveloperName == ug.DeveloperName))
                        {
                            entity.UserGroups.Remove(ug);
                        }
                    }

                    foreach (var ug in foundUserGroups)
                    {
                        if (!entity.UserGroups.Any(p => p.DeveloperName == ug.DeveloperName))
                        {
                            entity.UserGroups.Add(ug);
                        }
                    }
                }
            }

            entity.LastLoggedInTime = DateTime.UtcNow;
            entity.AuthenticationSchemeId = authScheme.Id;

            if (firstTime)
                _db.Users.Add(entity);

            if (jwtLogin != null)
                _db.JwtLogins.Add(jwtLogin);

            await _db.SaveChangesAsync(cancellationToken);
            return new CommandResponseDto<LoginDto>(
                new LoginDto
                {
                    Id = entity.Id,
                    FirstName = entity.FirstName,
                    LastName = entity.LastName,
                    EmailAddress = entity.EmailAddress,
                    LastModificationTime = entity.LastModificationTime,
                    AuthenticationScheme = authScheme.DeveloperName,
                    SsoId = entity.SsoId,
                    IsAdmin = entity.IsAdmin,
                }
            );
        }
    }
}
