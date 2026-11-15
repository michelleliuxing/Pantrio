using Microsoft.OpenApi;

namespace Pantrio.Api.Infrastructure;

public static class OpenApiSetup
{
    public static IServiceCollection AddPantrioOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(o =>
        {
            o.AddDocumentTransformer((doc, _, _) =>
            {
                doc.Info.Title = "Pantrio API";
                doc.Info.Version = "v1";
                return Task.CompletedTask;
            });

            // Request bodies: a nullable property may be left out entirely (PATCH semantics),
            // so the generated TypeScript marks it optional instead of required-but-null.
            o.AddSchemaTransformer((schema, ctx, _) =>
            {
                if (!ctx.JsonTypeInfo.Type.Name.EndsWith("Request", StringComparison.Ordinal)
                    || schema.Properties is null || schema.Required is null)
                {
                    return Task.CompletedTask;
                }

                foreach (var (name, prop) in schema.Properties)
                {
                    if (AllowsNull(prop))
                    {
                        schema.Required.Remove(name);
                    }
                }

                return Task.CompletedTask;
            });
        });

    private static bool AllowsNull(IOpenApiSchema schema) =>
        (schema.Type is { } t && t.HasFlag(JsonSchemaType.Null))
        || (schema.OneOf?.Any(AllowsNull) ?? false)
        || (schema.AnyOf?.Any(AllowsNull) ?? false);
}
