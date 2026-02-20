using System;
using System.Text;

public static class StringUtil
{
    public static bool IsNullOrWhiteSpace(string value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var builder = new StringBuilder();

        for (int i = 0; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]) && i > 0)
                builder.Append('_');

            builder.Append(char.ToLower(input[i]));
        }

        return builder.ToString();
    }

    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength) + "...";
    }
}