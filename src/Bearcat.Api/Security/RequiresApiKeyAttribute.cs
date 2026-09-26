using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresApiKeyAttribute()
    : TypeFilterAttribute(typeof(ApiKeyAuthorizationFilter));
