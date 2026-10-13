using System.Text.Json;
using System.Text.Json.Serialization;

namespace Users.Domain.Entities;

public sealed class FlexibleUserRoleConverter : JsonConverter<UserRole>
{
    public override UserRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var intVal = reader.GetInt32();
            return intVal == 1 ? UserRole.Admin : UserRole.User;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var strVal = reader.GetString();
            if (string.IsNullOrWhiteSpace(strVal))
                return UserRole.User;

            if (string.Equals(strVal, "Admin", StringComparison.OrdinalIgnoreCase) || strVal == "1")
                return UserRole.Admin;

            return UserRole.User;
        }

        return UserRole.User;
    }

    public override void Write(Utf8JsonWriter writer, UserRole value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
