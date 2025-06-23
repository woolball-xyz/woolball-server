using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Serialization;
using System.Reflection;
using Presentation.Models;

namespace WebApi;

/// <summary>
/// Schema filter to support System.Text.Json polymorphism with JsonPolymorphic and JsonDerivedType attributes
/// This addresses the lack of native support in Swashbuckle.AspNetCore for .NET 7+ polymorphic serialization
/// </summary>
public class JsonPolymorphicSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        
        // Check if the type has JsonPolymorphic attribute
        var polymorphicAttribute = type.GetCustomAttribute<JsonPolymorphicAttribute>();
        if (polymorphicAttribute == null)
            return;

        // Get all JsonDerivedType attributes
        var derivedTypeAttributes = type.GetCustomAttributes<JsonDerivedTypeAttribute>().ToArray();
        if (!derivedTypeAttributes.Any())
            return;

        // Create oneOf schema for polymorphic types
        schema.OneOf = new List<OpenApiSchema>();
        
        // Add discriminator configuration
        var discriminatorPropertyName = polymorphicAttribute.TypeDiscriminatorPropertyName ?? "$type";
        schema.Discriminator = new OpenApiDiscriminator
        {
            PropertyName = discriminatorPropertyName,
            Mapping = new Dictionary<string, string>()
        };

        // Add each derived type to oneOf and discriminator mapping
        foreach (var derivedTypeAttr in derivedTypeAttributes)
        {
            var derivedType = derivedTypeAttr.DerivedType;
            var typeDiscriminator = derivedTypeAttr.TypeDiscriminator?.ToString() ?? derivedType.Name;
            
            // Generate schema reference for the derived type
            var schemaReference = context.SchemaGenerator.GenerateSchema(derivedType, context.SchemaRepository);
            
            // Add to oneOf
            schema.OneOf.Add(new OpenApiSchema
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.Schema,
                    Id = derivedType.Name
                }
            });
            
            // Add to discriminator mapping
            schema.Discriminator.Mapping[typeDiscriminator] = $"#/components/schemas/{derivedType.Name}";
        }

        // Ensure the discriminator property is present in the schema
        if (schema.Properties == null)
            schema.Properties = new Dictionary<string, OpenApiSchema>();
            
        if (!schema.Properties.ContainsKey(discriminatorPropertyName))
        {
            schema.Properties[discriminatorPropertyName] = new OpenApiSchema
            {
                Type = "string",
                Description = "Type discriminator for polymorphic serialization"
            };
        }

        // Make discriminator property required
        if (schema.Required == null)
            schema.Required = new HashSet<string>();
        schema.Required.Add(discriminatorPropertyName);

        // Clear the regular properties since we're using oneOf
        // Keep only the discriminator property
        var discriminatorProperty = schema.Properties[discriminatorPropertyName];
        schema.Properties.Clear();
        schema.Properties[discriminatorPropertyName] = discriminatorProperty;
        
        // Clear type since we're using oneOf
        schema.Type = null;
    }
}

/// <summary>
/// Document filter to ensure proper oneOf generation for polymorphic request/response schemas
/// </summary>
public class JsonPolymorphicDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Find all polymorphic base types in the schemas
        var polymorphicSchemas = swaggerDoc.Components.Schemas
            .Where(kvp => HasPolymorphicAttribute(kvp.Key, context))
            .ToList();

        foreach (var (schemaName, schema) in polymorphicSchemas)
        {
            // Ensure the schema has proper oneOf structure
            if (schema.OneOf?.Any() == true && schema.Discriminator != null)
            {
                // Update all references to this schema in paths to use oneOf
                UpdatePathReferences(swaggerDoc, schemaName, schema);
            }
        }
    }

    private bool HasPolymorphicAttribute(string schemaName, DocumentFilterContext context)
    {
        // Try to find the type by name in the known types
        var type = context.ApiDescriptions
            .SelectMany(api => api.ParameterDescriptions)
            .Select(param => param.Type)
            .Concat(context.ApiDescriptions
                .SelectMany(api => api.SupportedResponseTypes)
                .Select(response => response.Type))
            .Where(t => t != null && t.Name == schemaName)
            .FirstOrDefault();

        return type?.GetCustomAttribute<JsonPolymorphicAttribute>() != null;
    }

    private void UpdatePathReferences(OpenApiDocument swaggerDoc, string schemaName, OpenApiSchema polymorphicSchema)
    {
        foreach (var path in swaggerDoc.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                // Update request body references
                if (operation.RequestBody?.Content != null)
                {
                    foreach (var content in operation.RequestBody.Content.Values)
                    {
                        UpdateSchemaReference(content.Schema, schemaName, polymorphicSchema);
                    }
                }

                // Update response references
                foreach (var response in operation.Responses.Values)
                {
                    if (response.Content != null)
                    {
                        foreach (var content in response.Content.Values)
                        {
                            UpdateSchemaReference(content.Schema, schemaName, polymorphicSchema);
                        }
                    }
                }
            }
        }
    }

    private void UpdateSchemaReference(OpenApiSchema schema, string targetSchemaName, OpenApiSchema polymorphicSchema)
    {
        if (schema?.Reference?.Id == targetSchemaName)
        {
            // Replace the simple reference with the polymorphic structure
            schema.OneOf = polymorphicSchema.OneOf;
            schema.Discriminator = polymorphicSchema.Discriminator;
            schema.Reference = null;
        }
    }
}