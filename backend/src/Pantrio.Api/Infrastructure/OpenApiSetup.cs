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

                // Request bodies: a nullable property may be left out entirely (PATCH semantics), so the generated
                // TypeScript marks it optional instead of required-but-null. Runs on the finished document because
                // nullable enum references are wrapped in oneOf only after schema transformers have run.
                foreach (var (name, schema) in doc.Components?.Schemas ?? new Dictionary<string, IOpenApiSchema>())
                {
                    if ((name.EndsWith("Request", StringComparison.Ordinal) || name.EndsWith("Input", StringComparison.Ordinal))
                        && schema is OpenApiSchema { Properties: { } props, Required: { } required })
                    {
                        foreach (var (prop, propSchema) in props)
                        {
                            if (AllowsNull(propSchema))
                            {
                                required.Remove(prop);
                            }
                        }
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
